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

    /// <summary>Mã website do VNPay cấp khi đăng ký tài khoản sandbox.</summary>
    public string TmnCode { get; set; } = string.Empty;

    /// <summary>Chuỗi bí mật dùng để ký. KHÔNG commit — đặt bằng user-secrets.</summary>
    public string HashSecret { get; set; } = string.Empty;

    public string PayUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    /// <summary>Địa chỉ API dùng cho truy vấn giao dịch và hoàn tiền.</summary>
    public string ApiUrl { get; set; } = "https://sandbox.vnpayment.vn/merchant_webapi/api/transaction";

    /// <summary>
    /// Gốc địa chỉ của chính API này, dùng để dựng vnp_ReturnUrl.
    /// Để trống thì lấy theo địa chỉ của request hiện tại.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Gốc địa chỉ của giao diện React để đưa khách quay lại sau khi thanh toán.
    /// Lúc dev là http://localhost:5111; khi đóng gói chung thì để trống.
    /// </summary>
    public string? AppBaseUrl { get; set; }

    public string Locale { get; set; } = "vn";

    /// <summary>
    /// Phương thức mặc định khi khách không chọn gì.
    /// Để trống là an toàn nhất: VNPay hiện trang chọn của họ, trong đó có cả quét QR.
    /// Ép sẵn "VNPAYQR" chỉ chạy được khi tài khoản merchant đã được bật kênh QR,
    /// tài khoản sandbox mới thường chưa có nên sẽ báo lỗi 76.
    /// </summary>
    public string DefaultMethod { get; set; } = string.Empty;

    /// <summary>Link thanh toán VNPay hết hạn sau bao nhiêu phút.</summary>
    public int ExpireMinutes { get; set; } = 15;

    /// <summary>
    /// Sandbox thường từ chối lệnh hoàn tiền vì tài khoản thử nghiệm không có số dư thật.
    /// Bật cờ này (mặc định) để luồng hủy vé chạy trọn vẹn khi demo mà vẫn giữ nguyên code gọi API thật.
    /// </summary>
    public bool SimulateRefund { get; set; } = true;
}

/// <summary>
/// Cổng thanh toán VNPay — bản sandbox, giao thức 2.1.0.
///
/// Ba điểm khác cổng giả lập, đều là đặc tính của cổng thật:
/// 1. Chữ ký HMAC-SHA512 tính trên chuỗi truy vấn đã sắp xếp và mã hoá URL (BR-07).
/// 2. Kết quả về theo HAI đường: đường khách quay lại (vnp_ReturnUrl) và IPN gọi
///    từ máy chủ VNPay. Cả hai cùng đi qua <see cref="Services.PaymentService"/>
///    nên xử lý trùng được chặn bởi BR-08 — đây chính là lý do phải làm idempotent.
/// 3. Mã giao dịch vnp_TxnRef phải duy nhất trên toàn bộ tài khoản merchant.
///
/// Lưu ý khi chạy ở localhost: máy chủ VNPay KHÔNG gọi được IPN vào máy cá nhân,
/// nên trên thực tế chỉ đường quay lại chạy. Vì vậy handler của đường quay lại
/// cũng ghi nhận thanh toán chứ không chỉ hiển thị.
/// </summary>
public class VnPayGateway(
    IOptions<VnPayOptions> options,
    IRequestContext request,
    IHttpClientFactory httpClientFactory,
    ILogger<VnPayGateway> logger) : IPaymentGateway
{
    private readonly VnPayOptions _opt = options.Value;

    public string Provider => "VNPay";
    public string DisplayName => "VNPay (sandbox)";

    /// <summary>
    /// Các phương thức VNPay hỗ trợ, phân biệt bằng vnp_BankCode.
    /// Phần tử đầu là mặc định của giao diện.
    ///
    /// Để trống đứng đầu vì đó là lựa chọn luôn chạy: VNPay mở trang chọn của họ,
    /// trong đó đã có sẵn mục quét mã QR. Ba lựa chọn còn lại đi thẳng vào một kênh
    /// cụ thể, chỉ dùng được khi kênh đó đã bật cho mã website (xem README).
    /// </summary>
    public IReadOnlyList<PaymentMethod> Methods { get; } =
    [
        new PaymentMethod("", "Trang VNPay (có cả QR)"),
        new PaymentMethod("VNPAYQR", "Đi thẳng vào quét mã QR"),
        new PaymentMethod("VNBANK", "Thẻ ATM / Internet Banking"),
        new PaymentMethod("INTCARD", "Thẻ quốc tế Visa, Master"),
    ];

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_opt.TmnCode) && !string.IsNullOrWhiteSpace(_opt.HashSecret);

    /// <summary>Nơi khách được đưa về sau khi rời cổng VNPay.</summary>
    public string AppBaseUrl =>
        (_opt.AppBaseUrl ?? _opt.PublicBaseUrl ?? request.BaseUrl ?? string.Empty).TrimEnd('/');

    // ------------------------------------------------------------ tạo link thanh toán

    public Task<PaymentIntent> CreateAsync(
        Booking booking, PaymentIntentRequest intent, CancellationToken ct = default)
    {
        if (!IsConfigured)
            throw new InvalidOperationException(
                "Chưa cấu hình Payment:VnPay:TmnCode và Payment:VnPay:HashSecret — xem README.");

        var createdAt = VnNow();

        // vnp_TxnRef phải duy nhất trên toàn tài khoản merchant, không chỉ trong một đơn:
        // khách thanh toán thất bại rồi thử lại phải sinh mã mới.
        // Chỉ lấy tới giây thì hai lần bấm liên tiếp trong cùng một giây sẽ trùng mã và
        // vi phạm ràng buộc duy nhất (Provider, ProviderTxnId), nên thêm hậu tố ngẫu nhiên.
        var txnRef = $"{booking.Code}-{createdAt:HHmmss}{CodeGenerator.New(string.Empty, 4)}";

        var baseUrl = (_opt.PublicBaseUrl ?? request.BaseUrl ?? string.Empty).TrimEnd('/');

        var data = new Dictionary<string, string>
        {
            ["vnp_Version"] = "2.1.0",
            ["vnp_Command"] = "pay",
            ["vnp_TmnCode"] = _opt.TmnCode,
            // VNPay nhận số tiền đã nhân 100 và không có phần thập phân.
            ["vnp_Amount"] = ((long)(booking.Total * 100m)).ToString(CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = "VND",
            ["vnp_TxnRef"] = txnRef,
            ["vnp_OrderInfo"] = $"Thanh toan don dat ve {booking.Code}",
            ["vnp_OrderType"] = "other",
            ["vnp_Locale"] = _opt.Locale,
            ["vnp_ReturnUrl"] = $"{baseUrl}/api/payments/vnpay-return",
            ["vnp_IpAddr"] = request.ClientIpAddress ?? "127.0.0.1",
            ["vnp_CreateDate"] = createdAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_ExpireDate"] = createdAt.AddMinutes(_opt.ExpireMinutes)
                                          .ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
        };

        // vnp_BankCode quyết định khách thấy màn hình nào: VNPAYQR ra thẳng mã QR,
        // VNBANK ra danh sách ngân hàng, để trống thì VNPay tự hỏi.
        // Chuỗi rỗng bị BuildQuery loại bỏ nên không cần xử lý riêng.
        var method = intent.MethodCode ?? _opt.DefaultMethod;
        if (Methods.Any(m => m.Code == method) && method.Length > 0)
            data["vnp_BankCode"] = method;

        var query = BuildQuery(data);
        var url = $"{_opt.PayUrl}?{query}&vnp_SecureHash={Sign(query)}";

        logger.LogInformation("Tạo link VNPay cho đơn {Code}, mã giao dịch {Txn}, phương thức {Method}",
            booking.Code, txnRef, string.IsNullOrEmpty(method) ? "(VNPay tự hỏi)" : method);
        return Task.FromResult(new PaymentIntent(txnRef, url));
    }

    // ------------------------------------------------------------ kiểm tra chữ ký

    /// <summary>
    /// BR-07: chỉ tin dữ liệu có chữ ký đúng. Chuỗi ký là toàn bộ tham số vnp_*
    /// trừ hai trường chữ ký, sắp xếp theo tên và mã hoá URL — đúng thứ tự VNPay quy định.
    /// </summary>
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

        // "00" ở cả hai trường mới là thành công. Đường quay lại đôi khi thiếu
        // vnp_TransactionStatus nên chỉ bắt buộc khi trường này có mặt.
        var succeeded = responseCode == "00"
                     && (string.IsNullOrEmpty(transactionStatus) || transactionStatus == "00");

        decimal.TryParse(Get(payload, "vnp_Amount"), NumberStyles.Integer,
                         CultureInfo.InvariantCulture, out var raw);

        var rawPayload = string.Join("&", payload
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key}={kv.Value}"));

        return new PaymentCallback(txnRef, succeeded, raw / 100m, rawPayload);
    }

    /// <summary>Diễn giải mã lỗi VNPay sang tiếng Việt để hiện cho khách.</summary>
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

    // ------------------------------------------------------------ hoàn tiền

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
            // 02 = hoàn toàn phần, 03 = hoàn một phần.
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

        // Lệnh hoàn tiền ký trên chuỗi nối bằng dấu | theo đúng thứ tự tài liệu VNPay,
        // KHÁC với cách ký của link thanh toán.
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

    // ------------------------------------------------------------ tiện ích

    /// <summary>Giờ Việt Nam — VNPay yêu cầu mọi mốc thời gian ở GMT+7.</summary>
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
