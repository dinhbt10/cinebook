using CineBook.Domain.Common;
using CineBook.Domain.Enums;

namespace CineBook.Domain.Entities;

public class Showtime : BaseEntity
{
    public Guid MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    public DateTime StartsAtUtc { get; set; }

    public DateTime EndsAtUtc { get; set; }

    public decimal BasePrice { get; set; }
    public SubtitleMode SubtitleMode { get; set; } = SubtitleMode.Subtitled;

    public bool IsOnSale { get; set; }
    public bool IsCancelled { get; set; }

    public ICollection<ShowtimeSeat> Seats { get; set; } = new List<ShowtimeSeat>();
}

public class ShowtimeSeat
{
    public Guid ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;
    public Guid SeatId { get; set; }
    public Seat Seat { get; set; } = null!;

    public SeatStatus Status { get; set; } = SeatStatus.Available;

    public Guid? HeldByUserId { get; set; }

    public DateTime? HoldExpiresAtUtc { get; set; }

    public Guid? BookingId { get; set; }
    public Booking? Booking { get; set; }

    public decimal Price { get; set; }

    public uint Version { get; set; }
}
