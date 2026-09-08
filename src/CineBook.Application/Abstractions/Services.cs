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

/// <summary>Ký và kiểm tra chữ ký nội dung mã QR (BR-11).</summary>
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

/// <summary>
/// Vài thông tin của request đang phục vụ mà cổng thanh toán cần biết:
/// VNPay bắt buộc gửi kèm địa chỉ IP của khách và một địa chỉ quay lại tuyệt đối.
/// Tách thành interface để tầng Application và Infrastructure không phải
/// phụ thuộc trực tiếp vào ASP.NET Core.
/// </summary>
public interface IRequestContext
{
    /// <summary>Địa chỉ IP của khách, null khi chạy ngoài ngữ cảnh HTTP.</summary>
    string? ClientIpAddress { get; }

    /// <summary>Gốc địa chỉ của request hiện tại, ví dụ "https://cinebook.example".</summary>
    string? BaseUrl { get; }
}

// ---------- Cổng thanh toán ----------

public record PaymentIntent(string ProviderTxnId, string PaymentUrl);

/// <summary>
/// Một cách trả tiền trong cùng một cổng. VNPay phân biệt quét mã QR, thẻ ATM nội địa
/// và thẻ quốc tế bằng tham số vnp_BankCode; chọn sẵn thì khách vào thẳng màn hình đó
/// thay vì phải bấm thêm một bước trên trang của cổng.
/// </summary>
public record PaymentMethod(string Code, string DisplayName);

/// <summary>Tham số tạo link thanh toán. Gói lại thành record để thêm tuỳ chọn mới không phải sửa interface.</summary>
public record PaymentIntentRequest(string ReturnUrl, string? MethodCode = null);

public record PaymentCallback(string ProviderTxnId, bool Succeeded, decimal Amount, string RawPayload);

/// <summary>
/// Một interface, nhiều cài đặt (Mock, VNPay…). Đổi bằng cấu hình —
/// đúng nguyên lý đảo ngược phụ thuộc, đưa thẳng vào sơ đồ lớp của báo cáo.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>Khoá định danh, dùng trong URL và lưu vào bảng Payments.</summary>
    string Provider { get; }

    /// <summary>Tên hiện cho khách chọn ở bước thanh toán.</summary>
    string DisplayName { get; }

    /// <summary>Cổng đã có đủ cấu hình để dùng chưa — thiếu khoá thì không hiện cho khách.</summary>
    bool IsConfigured { get; }

    /// <summary>Các cách trả tiền cổng này hỗ trợ. Phần tử đầu là mặc định.</summary>
    IReadOnlyList<PaymentMethod> Methods { get; }
    Task<PaymentIntent> CreateAsync(Booking booking, PaymentIntentRequest request, CancellationToken ct = default);
    bool VerifySignature(IDictionary<string, string> payload);
    PaymentCallback ParseCallback(IDictionary<string, string> payload);
    Task<bool> RefundAsync(Payment payment, decimal amount, CancellationToken ct = default);
}
