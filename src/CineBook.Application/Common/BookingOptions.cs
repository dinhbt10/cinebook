namespace CineBook.Application.Common;

/// <summary>
/// Tham số hệ thống — đưa ra cấu hình để đổi được mà không phải sửa code.
/// Mỗi tham số ứng với một ràng buộc nghiệp vụ trong tài liệu flow.
/// </summary>
public class BookingOptions
{
    public const string SectionName = "Booking";

    /// <summary>BR-02: thời gian giữ ghế.</summary>
    public int HoldMinutes { get; set; } = 8;

    /// <summary>BR-04: số ghế tối đa một lần đặt.</summary>
    public int MaxSeatsPerBooking { get; set; } = 8;

    /// <summary>BR-03: không cho đặt suất bắt đầu trong vòng bao nhiêu phút tới.</summary>
    public int MinutesBeforeStartToBlock { get; set; } = 15;

    /// <summary>BR-17: thời gian dọn phòng giữa hai suất.</summary>
    public int RoomCleanupMinutes { get; set; } = 15;

    /// <summary>BR-14: hủy vé trước giờ chiếu bao nhiêu giờ thì được hoàn tiền.</summary>
    public int RefundFullBeforeHours = 24;
    public int RefundPartialBeforeHours { get; set; } = 2;
    public decimal RefundPartialRate { get; set; } = 0.8m;

    /// <summary>Cho phép quét vé từ bao lâu trước giờ chiếu.</summary>
    public int CheckInOpenBeforeMinutes { get; set; } = 60;
    public int CheckInCloseAfterMinutes { get; set; } = 30;
}
