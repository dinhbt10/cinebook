using CineBook.Domain.Common;
using CineBook.Domain.Enums;

namespace CineBook.Domain.Entities;

public class Booking : BaseEntity
{
    /// <summary>Mã đặt vé khách đọc được, ví dụ CB8F2K9L.</summary>
    public string Code { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;

    public BookingStatus Status { get; set; } = BookingStatus.Pending;

    public decimal SeatsSubtotal { get; set; }
    public decimal ConcessionSubtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }

    public Guid? PromotionId { get; set; }
    public Promotion? Promotion { get; set; }

    /// <summary>BR-02, BR-09: mốc hết hạn giữ ghế, frontend dùng để đếm ngược.</summary>
    public DateTime HoldExpiresAtUtc { get; set; }

    public DateTime? PaidAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelReason { get; set; }

    public ICollection<ShowtimeSeat> BookedSeats { get; set; } = new List<ShowtimeSeat>();
    public ICollection<BookingCombo> Combos { get; set; } = new List<BookingCombo>();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class BookingCombo
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public Guid ComboId { get; set; }
    public Combo Combo { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

/// <summary>Một vé cho một ghế. Mã QR được ký để chống làm giả (BR-11).</summary>
public class Ticket : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public Guid SeatId { get; set; }
    public Seat Seat { get; set; } = null!;

    public string Code { get; set; } = null!;
    public string QrSignature { get; set; } = null!;
    public TicketStatus Status { get; set; } = TicketStatus.Valid;

    /// <summary>BR-12: một vé chỉ được check-in đúng một lần.</summary>
    public DateTime? CheckedInAtUtc { get; set; }
    public Guid? CheckedInByUserId { get; set; }
}

public class Payment : BaseEntity
{
    public Guid BookingId { get; set; }
    public Booking Booking { get; set; } = null!;

    /// <summary>"Mock", "VNPay", "MoMo"…</summary>
    public string Provider { get; set; } = null!;

    /// <summary>BR-08: mã giao dịch của cổng, có ràng buộc duy nhất để chống xử lý trùng.</summary>
    public string ProviderTxnId { get; set; } = null!;

    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string? RawPayload { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public decimal RefundedAmount { get; set; }
}
