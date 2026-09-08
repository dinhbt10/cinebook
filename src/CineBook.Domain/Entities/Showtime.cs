using CineBook.Domain.Common;
using CineBook.Domain.Enums;

namespace CineBook.Domain.Entities;

public class Showtime : BaseEntity
{
    public Guid MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    /// <summary>Luôn lưu ở UTC (rủi ro #5 trong đánh giá công nghệ).</summary>
    public DateTime StartsAtUtc { get; set; }

    /// <summary>BR-17: = StartsAtUtc + thời lượng phim + thời gian dọn phòng.</summary>
    public DateTime EndsAtUtc { get; set; }

    public decimal BasePrice { get; set; }
    public SubtitleMode SubtitleMode { get; set; } = SubtitleMode.Subtitled;

    /// <summary>Suất đã tạo nhưng chưa mở bán vé.</summary>
    public bool IsOnSale { get; set; }
    public bool IsCancelled { get; set; }

    public ICollection<ShowtimeSeat> Seats { get; set; } = new List<ShowtimeSeat>();
}

/// <summary>
/// Một dòng cho mỗi ghế của mỗi suất chiếu. Đây là bảng thực hiện việc giữ ghế
/// và chống đặt trùng (BR-01, BR-02) bằng một câu UPDATE có điều kiện,
/// không cần Redis — xem §6.1 tài liệu đánh giá công nghệ.
/// </summary>
public class ShowtimeSeat
{
    public Guid ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;
    public Guid SeatId { get; set; }
    public Seat Seat { get; set; } = null!;

    public SeatStatus Status { get; set; } = SeatStatus.Available;

    /// <summary>Ai đang giữ ghế này.</summary>
    public Guid? HeldByUserId { get; set; }

    /// <summary>Hết thời điểm này thì ghế coi như trống, không cần job nào can thiệp.</summary>
    public DateTime? HoldExpiresAtUtc { get; set; }

    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }

    /// <summary>Giá đã chốt cho ghế này tại thời điểm giữ (BR-18).</summary>
    public decimal Price { get; set; }

    public uint Version { get; set; }
}
