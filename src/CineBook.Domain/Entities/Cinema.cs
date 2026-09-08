using CineBook.Domain.Common;
using CineBook.Domain.Enums;

namespace CineBook.Domain.Entities;

public class Cinema : BaseEntity
{
    public string Name { get; set; } = null!;
    public string City { get; set; } = null!;
    public string? District { get; set; }
    public string Address { get; set; } = null!;
    public string? Hotline { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Room> Rooms { get; set; } = new List<Room>();
}

public class Room : BaseEntity
{
    public Guid CinemaId { get; set; }
    public Cinema Cinema { get; set; } = null!;
    public string Name { get; set; } = null!;
    public RoomType RoomType { get; set; } = RoomType.TwoD;
    public bool IsActive { get; set; } = true;

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
    public ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}

/// <summary>Ghế vật lý của một phòng chiếu. Không mang trạng thái bán — xem ShowtimeSeat.</summary>
public class Seat : BaseEntity
{
    public Guid RoomId { get; set; }
    public Room Room { get; set; } = null!;

    /// <summary>Hàng ghế: A, B, C…</summary>
    public string RowLabel { get; set; } = null!;
    public int Number { get; set; }
    public SeatType SeatType { get; set; } = SeatType.Standard;

    /// <summary>BR-06: ghế bảo trì không bao giờ được bán.</summary>
    public bool IsActive { get; set; } = true;

    public string Label => $"{RowLabel}{Number}";
}
