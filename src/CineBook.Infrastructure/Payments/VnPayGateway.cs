using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CineBook.Infrastructure.Payments;

public class VnPayOptions
{
    public const string SectionName = "Payment:VnPay";

    public string TmnCode { get; set; } = string.Empty;

    public string HashSecret { get; set; } = string.Empty;

    public string PayUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    public string ApiUrl { get; set; } = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";

    public string? PublicBaseUrl { get; set; }

    public string? AppBaseUrl { get; set; }

    public string Locale { get; set; } = "vn";

    public string DefaultMethod { get; set; } = string.Empty;

    public int ExpireMinutes { get; set; } = 15;

    public bool SimulateRefund { get; set; } = true;
}

public class VnPayGateway(
    IOptions<VnPayOptions> options,
    IRequestContext request,
    IHttpClientFactory httpClientFactory,
    ILogger<VnPayGateway> logger) : IPaymentGateway
{
    private readonly VnPayOptions _opt = options.Value;

    public string Provider => "VNPay";
    public string DisplayName => "VNPay (sandbox)";

    public IReadOnlyList<PaymentMethod> Methods { get; } =
    [
        new PaymentMethod("", "Trang VNPay (có cả QR)"),
        new PaymentMethod("VNPAYQR", "Đi thẳng vào quét mã QR"),
        new PaymentMethod("VNBANK", "Thẻ ATM / Internet Banking"),
        new PaymentMethod("INTCARD", "Thẻ quốc tế Visa, Master"),
    ];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_opt.TmnCode) && !string.IsNullOrWhiteSpace(_opt.HashSecret);

    public string AppBaseUrl =>
        (_opt.AppBaseUrl ?? _opt.PublicBaseUrl ?? request.BaseUrl ?? string.Empty).TrimEnd('/');

    public Task<PaymentIntent> CreateAsync(
        Booking booking, PaymentIntentRequest intent, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Chưa cấu hình Payment:VnPay:TmnCode và Payment:VnPay:HashSecret — xem README.");

        var createdAt = VnNow();

        var txnRef = $"{booking.Code}-{createdAt:HHmmss}{CodeGenerator.New(string.Empty, 4)}";

        var baseUrl = (_opt.PublicBaseUrl ?? request.BaseUrl ?? string.Empty).TrimEnd('/');

        var expireAt = createdAt.AddMinutes(_opt.ExpireMinutes);
        var holdEndsVn = booking.HoldExpiresAtUtc.AddHours(7);
        if (holdEndsVn < expireAt) expireAt = holdEndsVn;

        var data = new Dictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _opt.TmnCode,
            ["vnp_Amount"] = ((long)(booking.Total * 100m)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = txnRef,
            ["vnp_OrderInfo"] = $"Thanh toan don dat ve {booking.Code}",
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = _opt.Locale,
            ["vnp_ReturnUrl"] = $"{baseUrl}/api/payments/vnpay-return",
            ["vnp_IpAddr"] = request.ClientIpAddress ?? "127.0.0.1",
            ["vnp_CreateDate"] = createdAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_ExpireDate"] = expireAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
        };

        var method = intent.MethodCode ?? _opt.DefaultMethod;
        if (Methods.Any(m => m.Code == method) && method.Length > 0)
            data["vnp_BankCode"] = method;

        var query = BuildQuery(data);
        var url = $"{_opt.PayUrl}?{query}&vnp_SecureHash={Sign(query)}";

        logger.LogInformation("Tạo link VNPay cho đơn {Code}, mã giao dịch {Txn}, phương thức {Method}",
            booking.Code, txnRef, string.IsNullOrEmpty(method) ? "(VNPay tự hỏi)" : method);
        return Task.FromResult(new PaymentIntent(txnRef, url));
    }

    public bool VerifySignature(IDictionary<string, string> payload)
    {
        if (!payload.TryGetValue("vnp_SecureHash", out var received) || string.IsNullOrWhiteSpace(received))
            return false;

        var signed = payload
            .Where(kv => kv.Key.StartsWith("vnp_", StringComparison.Ordinal)
                      && kv.Key != "vnp_SecureHash" && kv.Key != "vnp_SecureHashType");

        var expected = Sign(BuildQuery(signed));

        var ok = string.Equals(expected, received, StringComparison.OrdinalIgnoreCase);
        if (!ok)
            logger.LogWarning("Chữ ký VNPay không khớp cho giao dịch {Txn}",
                payload.TryGetValue("vnp_TxnRef", out var t) ? t : "?");
        return ok;
    }

    public PaymentCallback ParseCallback(IDictionary<string, string> payload)
    {
        var txnRef = Get(payload, "vnp_TxnRef");
        var responseCode = Get(payload, "vnp_ResponseCode");
        var transactionStatus = Get(payload, "vnp_TransactionStatus");

        var succeeded = responseCode == "00"
                     && (string.IsNullOrEmpty(transactionStatus) || transactionStatus == "00");

        decimal.TryParse(Get(payload, "vnp_Amount"), NumberStyles.Integer,
                         CultureInfo.InvariantCulture, out var raw);

        var rawPayload = string.Join("&", payload
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        return new PaymentCallback(txnRef, succeeded, raw / 100m, rawPayload);
    }

    public static string DescribeResponseCode(string? code) => code switch
    {
        "00" => "Giao dịch thành công.",
        "07" => "Giao dịch bị nghi ngờ gian lận.",
        "09" => "Thẻ chưa đăng ký dịch vụ InternetBanking.",
        "10" => "Xác thực thông tin thẻ sai quá 3 lần.",
        "11" => "Đã hết hạn chờ thanh toán.",
        "12" => "Thẻ hoặc tài khoản đang bị khoá.",
        "13" => "Nhập sai mật khẩu xác thực OTP.",
        "24" => "Khách hàng đã hủy giao dịch.",
        "51" => "Tài khoản không đủ số dư.",
        "65" => "Tài khoản đã vượt hạn mức giao dịch trong ngày.",
        "75" => "Ngân hàng thanh toán đang bảo trì.",
        "79" => "Nhập sai mật khẩu thanh toán quá số lần quy định.",
        _ => "Giao dịch không thành công.",
    };

    public async Task<bool> RefundAsync(Payment payment, decimal amount, CancellationToken ct = default)
    {
        if (_opt.SimulateRefund)
        {
            logger.LogInformation(
                "VNPay: mô phỏng hoàn {Amount:N0}đ cho giao dịch {Txn} " +
                "(Payment:VnPay:SimulateRefund = true, sandbox không hoàn tiền thật)",
                amount, payment.ProviderTxnId);
            return true;
        }

        var original = ParseRaw(payment.RawPayload);
        var createdAt = VnNow();
        var requestId = Guid.NewGuid().ToString("N")[..16];

        var body = new Dictionary<string, string>
        {
            ["vnp_RequestId"] = requestId,
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "refund",
            ["vnp_TmnCode"] = _opt.TmnCode,
            ["vnp_TransactionType"] = amount >= payment.Amount ? "02" : "03",
            ["vnp_TxnRef"] = payment.ProviderTxnId,
            ["vnp_Amount"] = ((long)(amount * 100m)).ToString(CultureInfo.InvariantCulture),
            ["vnp_TransactionNo"] = Get(original, "vnp_TransactionNo"),
            ["vnp_TransactionDate"] = Get(original, "vnp_PayDate"),
            ["vnp_CreateBy"] = "CineBook",
            ["vnp_CreateDate"] = createdAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_IpAddr"] = request.ClientIpAddress ?? "127.0.0.1",
            ["vnp_OrderInfo"] = $"Hoan tien don {payment.BookingId}",
        };

        string[] order =
        [
            "vnp_RequestId", "vnp_Version", "vnp_Command", "vnp_TmnCode", "vnp_TransactionType",
            "vnp_TxnRef", "vnp_Amount", "vnp_TransactionNo", "vnp_TransactionDate",
            "vnp_CreateBy", "vnp_CreateDate", "vnp_IpAddr", "vnp_OrderInfo",
        ];
        body["vnp_SecureHash"] = Sign(string.Join("|", order.Select(k => Get(body, k))));

        try
        {
            var http = httpClientFactory.CreateClient(nameof(VnPayGateway));
            using var response = await http.PostAsJsonAsync(_opt.ApiUrl, body, ct);
            var text = await response.Content.ReadAsStringAsync(ct);

            logger.LogInformation("VNPay trả lời lệnh hoàn tiền {Txn}: {Body}", payment.ProviderTxnId, text);

            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.TryGetProperty("vnp_ResponseCode", out var code)
                && code.GetString() == "00";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Gọi API hoàn tiền VNPay thất bại cho giao dịch {Txn}", payment.ProviderTxnId);
            return false;
        }
    }

    private static DateTime VnNow() => DateTime.UtcNow.AddHours(7);

    private static string BuildQuery(IEnumerable<KeyValuePair<string, string>> data) =>
        string.Join("&", data
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{WebUtility.UrlEncode(kv.Key)}={WebUtility.UrlEncode(kv.Value)}"));

    private string Sign(string data)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_opt.HashSecret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private static string Get(IDictionary<string, string> d, string key) =>
        d.TryGetValue(key, out var v) ? v : string.Empty;

    private static Dictionary<string, string> ParseRaw(string? raw)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(raw)) return result;

        foreach (var pair in raw.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var i = pair.IndexOf('=');
            if (i > 0) result[pair[..i]] = pair[(i + 1)..];
        }
        return result;
    }
}
