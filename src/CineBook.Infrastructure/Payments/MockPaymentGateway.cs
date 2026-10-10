using System.Globalization;
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

    public string PayPageUrl { get; set; } = "/mock-gateway";

    public bool Enabled { get; set; } = false;
}

public class MockPaymentGateway(IOptions<MockGatewayOptions> options) : IPaymentGateway
{
    private readonly MockGatewayOptions _opt = options.Value;

    public string Provider => "Mock";
    public string DisplayName => "Cổng giả lập (demo)";
    public bool IsConfigured => _opt.Enabled;

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
        decimal.TryParse(Get(payload, "amount", "0"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount);

        var raw = string.Join("&", payload.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
        return new PaymentCallback(txn, status.Equals("success", StringComparison.OrdinalIgnoreCase), amount, raw);
    }

    public Task<bool> RefundAsync(Payment payment, decimal amount, CancellationToken ct = default)
        => Task.FromResult(true);

    private static string Get(IDictionary<string, string> d, string key, string fallback) =>
        d.TryGetValue(key, out var v) ? v : fallback;

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
