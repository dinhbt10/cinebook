using System.Security.Cryptography;
using System.Text;
using CineBook.Application.Abstractions;
using CineBook.Domain.Entities;
using Microsoft.Extensions.Options;

namespace CineBook.Infrastructure.Payments;

public class MockGatewayOptions
{
    public const string SectionName = "Payment:Mock";
    public string Secret { get; set; } = "mock-gateway-secret";

    /// <summary>Địa chỉ trang thanh toán giả lập do chính API này phục vụ.</summary>
    public string PayPageUrl { get; set; } = "/mock-gateway";
}

/// <summary>
/// Cổng thanh toán giả lập. Giữ nguyên mọi đặc tính của cổng thật:
/// có chữ ký để kiểm tra (BR-07), có mã giao dịch riêng để chống xử lý trùng (BR-08),
/// và kết quả trả về bằng webhook chứ không phải bằng đường người dùng quay lại.
///
/// Nhờ vậy khi demo có thể chủ động bấm gửi webhook hai lần để chứng minh
/// hệ thống chỉ ghi nhận một lần — điều rất khó làm với cổng thật.
/// </summary>
public class MockPaymentGateway(IOptions<MockGatewayOptions> options) : IPaymentGateway
{
    private readonly MockGatewayOptions _opt = options.Value;

    public string Provider => "Mock";
    public string DisplayName => "Cổng giả lập (demo)";
    public bool IsConfigured => true;

    public IReadOnlyList<PaymentMethod> Methods { get; } =
        [new PaymentMethod("", "Mô phỏng")];

    public Task<PaymentIntent> CreateAsync(Booking booking, PaymentIntentRequest request, CancellationToken ct = default)
    {
        var txnId = $"MOCK{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
        var url = $"{_opt.PayPageUrl}?txn={Uri.EscapeDataString(txnId)}" +
                  $"&amount={booking.Total:0}" +
                  $"&code={Uri.EscapeDataString(booking.Code)}" +
                  $"&returnUrl={Uri.EscapeDataString(request.ReturnUrl)}";

        return Task.FromResult(new PaymentIntent(txnId, url));
    }

    public bool VerifySignature(IDictionary<string, string> payload)
    {
        if (!payload.TryGetValue("signature", out var signature)) return false;
        return string.Equals(signature, ComputeSignature(payload), StringComparison.Ordinal);
    }

    public PaymentCallback ParseCallback(IDictionary<string, string> payload)
    {
        var txn = Get(payload, "txn", string.Empty);
        var status = Get(payload, "status", "failed");
        decimal.TryParse(Get(payload, "amount", "0"), out var amount);

        var raw = string.Join("&", payload.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
        return new PaymentCallback(txn, status.Equals("success", StringComparison.OrdinalIgnoreCase), amount, raw);
    }

    public Task<bool> RefundAsync(Payment payment, decimal amount, CancellationToken ct = default)
        => Task.FromResult(true);

    private static string Get(IDictionary<string, string> d, string key, string fallback) =>
        d.TryGetValue(key, out var v) ? v : fallback;

    /// <summary>Chữ ký tính trên mọi trường trừ chính trường signature, sắp xếp theo tên.</summary>
    public string ComputeSignature(IDictionary<string, string> payload)
    {
        var data = string.Join("&", payload
            .Where(k => k.Key != "signature")
            .OrderBy(k => k.Key, StringComparer.Ordinal)
            .Select(k => $"{k.Key}={k.Value}"));

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opt.Secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }
}
