using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Application.Dtos;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Application.Services;

/// <summary>
/// Tạo phòng chiếu bằng số hàng × số ghế mỗi hàng và sinh ghế tự động —
/// bản đơn giản thay cho trình thiết kế sơ đồ ghế kéo thả (§9.2 đánh giá công nghệ).
/// </summary>
public class RoomService(IAppDbContext db)
{
    public const int MaxRows = 26;

    public async Task<Result<Guid>> CreateAsync(CreateRoomRequest req, CancellationToken ct = default)
    {
        if (req.RowCount is < 1 or > MaxRows)
            return Result<Guid>.Fail("BAD_ROWS", $"Số hàng ghế phải từ 1 đến {MaxRows}.");
        if (req.SeatsPerRow is < 1 or > 40)
            return Result<Guid>.Fail("BAD_SEATS", "Số ghế mỗi hàng phải từ 1 đến 40.");

        if (!await db.Cinemas.AnyAsync(c => c.Id == req.CinemaId, ct))
            return Result<Guid>.Fail("CINEMA_NOT_FOUND", "Không tìm thấy rạp.");

        var room = new Room
        {
            CinemaId = req.CinemaId,
            Name = req.Name.Trim(),
            RoomType = req.RoomType
        };
        db.Rooms.Add(room);

        foreach (var seat in BuildSeats(room.Id, req.RowCount, req.SeatsPerRow, req.VipFromRow, req.VipToRow))
            db.Seats.Add(seat);

        await db.SaveChangesAsync(ct);
        return Result<Guid>.Ok(room.Id);
    }

    public static IEnumerable<Seat> BuildSeats(
        Guid roomId, int rowCount, int seatsPerRow, int vipFromRow, int vipToRow)
    {
        for (var r = 0; r < rowCount; r++)
        {
            var label = ((char)('A' + r)).ToString();
            var isVipRow = r + 1 >= vipFromRow && r + 1 <= vipToRow;

            // Hàng cuối cùng là ghế đôi — quy ước phổ biến của rạp Việt Nam.
            // Mỗi ghế đôi chiếm chỗ của hai ghế thường nên hàng này chỉ có một nửa số ghế.
            var isCoupleRow = r == rowCount - 1 && rowCount > 1;
            var count = isCoupleRow ? Math.Max(1, seatsPerRow / 2) : seatsPerRow;

            for (var n = 1; n <= count; n++)
            {
                yield return new Seat
                {
                    RoomId = roomId,
                    RowLabel = label,
                    Number = n,
                    SeatType = isCoupleRow ? SeatType.Couple
                             : isVipRow ? SeatType.Vip
                             : SeatType.Standard
                };
            }
        }
    }

    /// <summary>BR-06: đánh dấu ghế hỏng, ghế này sẽ không được đưa vào suất chiếu mới.</summary>
    public async Task<Result> SetSeatMaintenanceAsync(Guid seatId, bool broken, CancellationToken ct = default)
    {
        var seat = await db.Seats.FirstOrDefaultAsync(s => s.Id == seatId, ct);
        if (seat is null) return Result.Fail("NOT_FOUND", "Không tìm thấy ghế.");
        seat.IsActive = !broken;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<IReadOnlyList<RoomDto>> GetByCinemaAsync(Guid cinemaId, CancellationToken ct = default) =>
        await db.Rooms.AsNoTracking()
            .Where(r => r.CinemaId == cinemaId && r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new RoomDto(r.Id, r.Name, r.RoomType.ToString(), r.Seats.Count))
            .ToListAsync(ct);
}
