using CineBook.Domain.Entities;

namespace CineBook.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface ITokenService
{
    string CreateAccessToken(User user);
}

public interface IQrSigner
{
    string Sign(string ticketCode);
    bool Verify(string ticketCode, string signature);
    string BuildPayload(string ticketCode);
    bool TryParsePayload(string payload, out string ticketCode);
}

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
}

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

public interface IRequestContext
{
    string? ClientIpAddress { get; }

    string? BaseUrl { get; }
}

public record PaymentIntent(string ProviderTxnId, string PaymentUrl);

public record PaymentMethod(string Code, string DisplayName);

public record PaymentIntentRequest(string ReturnUrl, string? MethodCode = null);

public record PaymentCallback(string ProviderTxnId, bool Succeeded, decimal Amount, string RawPayload);

public interface IPaymentGateway
{
    string Provider { get; }

    string DisplayName { get; }

    bool IsConfigured { get; }

    IReadOnlyList<PaymentMethod> Methods { get; }
    Task<PaymentIntent> CreateAsync(Booking booking, PaymentIntentRequest request, CancellationToken ct = default);
    bool VerifySignature(IDictionary<string, string> payload);
    PaymentCallback ParseCallback(IDictionary<string, string> payload);
    Task<bool> RefundAsync(Payment payment, decimal amount, CancellationToken ct = default);
}
