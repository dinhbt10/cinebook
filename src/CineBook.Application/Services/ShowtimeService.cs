using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Application.Dtos;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CineBook.Application.Services;

public class ShowtimeService(
    IAppDbContext db,
    PricingService pricing,
    IDateTimeProvider clock,
    IOptions<BookingOptions> options)
{
    private readonly BookingOptions _opt = options.Value;

    /// <summary>
    /// Tạo suất chiếu. Áp BR-16 (không chồng giờ trong cùng phòng),
    /// BR-17 (tự tính giờ kết thúc) và sinh sẵn một dòng ShowtimeSeat cho mỗi ghế
    /// với giá đã chốt theo BR-18.
    /// </summary>
    public async Task<Result<Guid>> CreateAsync(CreateShowtimeRequest req, CancellationToken ct = default)
    {
        var movie = await db.Movies.FirstOrDefaultAsync(m => m.Id == req.MovieId, ct);
        if (movie is null) return Result<Guid>.Fail("MOVIE_NOT_FOUND", "Không tìm thấy phim.");
        if (movie.Status == MovieStatus.Ended)
            return Result<Guid>.Fail("MOVIE_ENDED", "Phim đã kết thúc lịch chiếu.");

        var room = await db.Rooms.Include(r => r.Cinema)
            .FirstOrDefaultAsync(r => r.Id == req.RoomId, ct);
        if (room is null) return Result<Guid>.Fail("ROOM_NOT_FOUND", "Không tìm thấy phòng chiếu.");

        if (req.StartsAtUtc <= clock.UtcNow)
            return Result<Guid>.Fail("START_IN_PAST", "Giờ chiếu phải ở tương lai.");

        // BR-17: giờ kết thúc = giờ bắt đầu + thời lượng phim + thời gian dọn phòng.
        var endsAtUtc = req.StartsAtUtc
            .AddMinutes(movie.DurationMinutes)
            .AddMinutes(_opt.RoomCleanupMinutes);

        // BR-16: một phòng không được có hai suất chồng giờ.
        var overlapped = await db.Showtimes.AsNoTracking()
            .Where(s => s.RoomId == req.RoomId && !s.IsCancelled)
            .Where(s => s.StartsAtUtc < endsAtUtc && s.EndsAtUtc > req.StartsAtUtc)
            .Select(s => new { s.Id, s.StartsAtUtc, s.EndsAtUtc })
            .FirstOrDefaultAsync(ct);

        if (overlapped is not null)
            return Result<Guid>.Fail("SHOWTIME_OVERLAP",
                $"Phòng này đã có suất chiếu từ {ToLocal(overlapped.StartsAtUtc):HH:mm dd/MM} " +
                $"đến {ToLocal(overlapped.EndsAtUtc):HH:mm}.");

        var showtime = new Showtime
        {
            MovieId = movie.Id,
            RoomId = room.Id,
            StartsAtUtc = req.StartsAtUtc,
            EndsAtUtc = endsAtUtc,
            BasePrice = req.BasePrice,
            SubtitleMode = req.SubtitleMode,
            IsOnSale = req.OpenForSale
        };
        db.Showtimes.Add(showtime);

        // Sinh sẵn ghế cho suất: mỗi ghế đang hoạt động một dòng, giá chốt luôn.
        var seats = await db.Seats.AsNoTracking()
            .Where(s => s.RoomId == room.Id && s.IsActive)
            .ToListAsync(ct);

        if (seats.Count == 0)
            return Result<Guid>.Fail("ROOM_HAS_NO_SEAT", "Phòng chiếu chưa có ghế nào.");

        var rules = await pricing.LoadActiveRulesAsync(ct);
        foreach (var seat in seats)
        {
            db.ShowtimeSeats.Add(new ShowtimeSeat
            {
                ShowtimeId = showtime.Id,
                SeatId = seat.Id,
                Status = SeatStatus.Available,
                Price = pricing.CalculateSeatPrice(
                    req.BasePrice, req.StartsAtUtc, room.CinemaId,
                    room.RoomType, seat.SeatType, rules)
            });
        }

        await db.SaveChangesAsync(ct);
        return Result<Guid>.Ok(showtime.Id);
    }

    public async Task<Result> SetOnSaleAsync(Guid showtimeId, bool onSale, CancellationToken ct = default)
    {
        var st = await db.Showtimes.FirstOrDefaultAsync(s => s.Id == showtimeId, ct);
        if (st is null) return Result.Fail("NOT_FOUND", "Không tìm thấy suất chiếu.");
        if (st.IsCancelled) return Result.Fail("CANCELLED", "Suất chiếu đã bị hủy.");
        st.IsOnSale = onSale;
        st.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    /// <summary>Danh sách suất chiếu của một phim theo ngày (giờ Việt Nam).</summary>
    public async Task<IReadOnlyList<ShowtimeListItemDto>> SearchAsync(
        Guid? movieId, Guid? cinemaId, DateOnly? localDate, CancellationToken ct = default)
    {
        var q = db.Showtimes.AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Room).ThenInclude(r => r.Cinema)
            .Where(s => !s.IsCancelled && s.IsOnSale);

        if (movieId is { } mid) q = q.Where(s => s.MovieId == mid);
        if (cinemaId is { } cid) q = q.Where(s => s.Room.CinemaId == cid);

        if (localDate is { } d)
        {
            var fromUtc = PricingService.LocalDateStartUtc(d);
            var toUtc = fromUtc.AddDays(1);
            q = q.Where(s => s.StartsAtUtc >= fromUtc && s.StartsAtUtc < toUtc);
        }

        // BR-03: ẩn suất đã qua giờ hoặc sắp bắt đầu.
        var nowUtc = clock.UtcNow;
        var cutoff = nowUtc.AddMinutes(_opt.MinutesBeforeStartToBlock);
        q = q.Where(s => s.StartsAtUtc > cutoff);

        return await q.OrderBy(s => s.StartsAtUtc)
            .Select(s => new ShowtimeListItemDto(
                s.Id, s.MovieId, s.Movie.Title, s.RoomId, s.Room.Name,
                s.Room.Cinema.Name, s.Room.RoomType.ToString(),
                s.StartsAtUtc, s.EndsAtUtc, s.SubtitleMode.ToString(),
                s.Seats.Min(x => (decimal?)x.Price) ?? 0m,
                s.Seats.Count(x => x.Status == SeatStatus.Available
                                   || (x.Status == SeatStatus.Held && x.HoldExpiresAtUtc < nowUtc)),
                s.Seats.Count,
                s.IsOnSale))
            .ToListAsync(ct);
    }

    /// <summary>BR-15: hủy suất chiếu thì mọi vé đã bán được hoàn 100%.</summary>
    public async Task<Result<int>> CancelAsync(Guid showtimeId, string reason, CancellationToken ct = default)
    {
        var st = await db.Showtimes.FirstOrDefaultAsync(s => s.Id == showtimeId, ct);
        if (st is null) return Result<int>.Fail("NOT_FOUND", "Không tìm thấy suất chiếu.");

        st.IsCancelled = true;
        st.IsOnSale = false;
        st.UpdatedAtUtc = clock.UtcNow;

        var affected = await db.Bookings
            .Where(b => b.ShowtimeId == showtimeId && b.Status == BookingStatus.Paid)
            .ToListAsync(ct);

        foreach (var b in affected)
        {
            b.Status = BookingStatus.Cancelled;
            b.CancelledAtUtc = clock.UtcNow;
            b.CancelReason = $"Rạp hủy suất chiếu: {reason}";
        }

        db.AuditLogs.Add(new AuditLog
        {
            Action = "SHOWTIME_CANCELLED",
            EntityType = nameof(Showtime),
            EntityId = showtimeId.ToString(),
            Note = $"{reason} — ảnh hưởng {affected.Count} đơn đã thanh toán"
        });

        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(affected.Count);
    }

    private static DateTime ToLocal(DateTime utc) => utc.AddHours(PricingService.VietnamUtcOffsetHours);
}
