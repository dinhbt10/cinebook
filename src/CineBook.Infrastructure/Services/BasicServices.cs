using System.Security.Cryptography;
using System.Text;
using CineBook.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CineBook.Infrastructure.Services;

public class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

    public bool Verify(string password, string hash)
    {
        try { return BCrypt.Net.BCrypt.Verify(password, hash); }
        catch { return false; }
    }
}

public class QrOptions
{
    public const string SectionName = "Qr";
    public string Secret { get; set; } = "doi-chuoi-nay-trong-cau-hinh-that-cua-ban";
}

/// <summary>
/// BR-11: nội dung mã QR có dạng "MÃ_VÉ.CHỮ_KÝ", chữ ký là HMAC-SHA256 của mã vé.
/// Không có khoá bí mật thì không tạo được mã QR hợp lệ, nên vé không thể làm giả.
/// </summary>
public class HmacQrSigner(IOptions<QrOptions> options) : IQrSigner
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.Secret);

    public string Sign(string ticketCode)
    {
        using var hmac = new HMACSHA256(_key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(ticketCode));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public bool Verify(string ticketCode, string signature)
    {
        var expected = Sign(ticketCode);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }

    public string BuildPayload(string ticketCode) => $"{ticketCode}.{Sign(ticketCode)}";

    public bool TryParsePayload(string payload, out string ticketCode)
    {
        ticketCode = string.Empty;
        if (string.IsNullOrWhiteSpace(payload)) return false;

        var parts = payload.Split('.', 2);
        if (parts.Length != 2) return false;
        if (!Verify(parts[0], parts[1])) return false;

        ticketCode = parts[0];
        return true;
    }
}

/// <summary>
/// Bản gửi mail cho môi trường học tập: ghi ra log thay vì gửi thật,
/// nên demo được toàn bộ luồng mà không cần dịch vụ bên ngoài.
/// </summary>
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        logger.LogInformation("📧 Gửi mail tới {To} — {Subject}\n{Body}", to, subject, htmlBody);
        return Task.CompletedTask;
    }
}
