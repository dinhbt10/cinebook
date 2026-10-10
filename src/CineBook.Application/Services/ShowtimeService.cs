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
    PaymentService payments,
    IDateTimeProvider clock,
    IOptions<BookingOptions> options)
{
    private readonly BookingOptions _opt = options.Value;

    public async Task<Result<Guid>> CreateAsync(
        CreateShowtimeRequest req, Guid? scopeCinemaId = null, CancellationToken ct = default)
    {
        var movie = await db.Movies.FirstOrDefaultAsync(m => m.Id == req.MovieId, ct);
        if (movie is null) return Result<Guid>.Fail("MOVIE_NOT_FOUND", "Không tìm thấy phim.");
        if (movie.Status == MovieStatus.Ended)
            return Result<Guid>.Fail("MOVIE_ENDED", "Phim đã kết thúc lịch chiếu.");

        var room = await db.Rooms.Include(r => r.Cinema)
            .FirstOrDefaultAsync(r => r.Id == req.RoomId, ct);
        if (room is null) return Result<Guid>.Fail("ROOM_NOT_FOUND", "Không tìm thấy phòng chiếu.");

        if (scopeCinemaId is { } scope && room.CinemaId != scope)
            return Result<Guid>.Fail(ForbiddenCinema, ForbiddenCinemaMessage);

        if (req.StartsAtUtc <= clock.UtcNow)
            return Result<Guid>.Fail("START_IN_PAST", "Giờ chiếu phải ở tương lai.");

        if (req.SeatPrices is { } p && (p.Standard <= 0 || p.Vip <= 0 || p.Couple <= 0))
            return Result<Guid>.Fail("BAD_PRICE", "Giá vé của mọi loại ghế phải lớn hơn 0.");

        var endsAtUtc = req.StartsAtUtc
            .AddMinutes(movie.DurationMinutes)
            .AddMinutes(_opt.RoomCleanupMinutes);

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
                Price = req.SeatPrices is { } prices
                    ? PriceOf(prices, seat.SeatType)
                    : pricing.CalculateSeatPrice(
                        req.BasePrice, req.StartsAtUtc, room.CinemaId,
                        room.RoomType, seat.SeatType, rules)
            });
        }

        await db.SaveChangesAsync(ct);
        return Result<Guid>.Ok(showtime.Id);
    }

    public async Task<Result<SeatTypePrices>> PreviewPricesAsync(
        Guid roomId, DateTime startsAtUtc, decimal basePrice, CancellationToken ct = default)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct);
        if (room is null) return Result<SeatTypePrices>.Fail("ROOM_NOT_FOUND", "Không tìm thấy phòng chiếu.");

        var utc = startsAtUtc.Kind == DateTimeKind.Local
            ? startsAtUtc.ToUniversalTime()
            : DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc);

        var rules = await pricing.LoadActiveRulesAsync(ct);
        decimal Price(SeatType type) =>
            pricing.CalculateSeatPrice(basePrice, utc, room.CinemaId, room.RoomType, type, rules);

        return Result<SeatTypePrices>.Ok(
            new SeatTypePrices(Price(SeatType.Standard), Price(SeatType.Vip), Price(SeatType.Couple)));
    }

    private static decimal PriceOf(SeatTypePrices prices, SeatType type) => type switch
    {
        SeatType.Vip => prices.Vip,
        SeatType.Couple => prices.Couple,
        _ => prices.Standard,
    };

    public const string ForbiddenCinema = "FORBIDDEN_CINEMA";
    private const string ForbiddenCinemaMessage = "Bạn chỉ quản lý được suất chiếu của rạp mình phụ trách.";

    public async Task<Result> SetOnSaleAsync(
        Guid showtimeId, bool onSale, Guid? scopeCinemaId = null, CancellationToken ct = default)
    {
        var st = await db.Showtimes.Include(s => s.Room).FirstOrDefaultAsync(s => s.Id == showtimeId, ct);
        if (st is null) return Result.Fail("NOT_FOUND", "Không tìm thấy suất chiếu.");
        if (scopeCinemaId is { } scope && st.Room.CinemaId != scope)
            return Result.Fail(ForbiddenCinema, ForbiddenCinemaMessage);
        if (st.IsCancelled) return Result.Fail("CANCELLED", "Suất chiếu đã bị hủy.");
        st.IsOnSale = onSale;
        st.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

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
                s.IsOnSale,
                s.IsCancelled))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ShowtimeListItemDto>> SearchForAdminAsync(
        Guid? cinemaId, DateOnly localDate, Guid? scopeCinemaId = null, CancellationToken ct = default)
    {
        var fromUtc = PricingService.LocalDateStartUtc(localDate);
        var toUtc = fromUtc.AddDays(1);
        var nowUtc = clock.UtcNow;

        var q = db.Showtimes.AsNoTracking()
            .Where(s => s.StartsAtUtc >= fromUtc && s.StartsAtUtc < toUtc);

        var cid = scopeCinemaId ?? cinemaId;
        if (cid is { } c) q = q.Where(s => s.Room.CinemaId == c);

        return await q.OrderBy(s => s.StartsAtUtc)
            .Select(s => new ShowtimeListItemDto(
                s.Id, s.MovieId, s.Movie.Title, s.RoomId, s.Room.Name,
                s.Room.Cinema.Name, s.Room.RoomType.ToString(),
                s.StartsAtUtc, s.EndsAtUtc, s.SubtitleMode.ToString(),
                s.Seats.Min(x => (decimal?)x.Price) ?? 0m,
                s.Seats.Count(x => x.Status == SeatStatus.Available
                                   || (x.Status == SeatStatus.Held && x.HoldExpiresAtUtc < nowUtc)),
                s.Seats.Count,
                s.IsOnSale,
                s.IsCancelled))
            .ToListAsync(ct);
    }

    public async Task<Result<int>> CancelAsync(
        Guid showtimeId, string reason, Guid? scopeCinemaId = null, Guid? actorUserId = null,
        CancellationToken ct = default)
    {
        var st = await db.Showtimes.Include(s => s.Room).FirstOrDefaultAsync(s => s.Id == showtimeId, ct);
        if (st is null) return Result<int>.Fail("NOT_FOUND", "Không tìm thấy suất chiếu.");
        if (scopeCinemaId is { } scope && st.Room.CinemaId != scope)
            return Result<int>.Fail(ForbiddenCinema, ForbiddenCinemaMessage);
        if (st.IsCancelled) return Result<int>.Fail("CANCELLED", "Suất chiếu đã bị hủy trước đó.");

        var now = clock.UtcNow;
        st.IsCancelled = true;
        st.IsOnSale = false;
        st.UpdatedAtUtc = now;

        var affected = await db.Bookings
            .Include(b => b.Tickets)
            .Where(b => b.ShowtimeId == showtimeId
                        && (b.Status == BookingStatus.Paid || b.Status == BookingStatus.Pending))
            .ToListAsync(ct);

        var paidCount = 0;
        var refundFailed = 0;
        foreach (var b in affected)
        {
            b.CancelledAtUtc = now;
            b.CancelReason = $"Rạp hủy suất chiếu: {reason}";
            foreach (var t in b.Tickets) t.Status = TicketStatus.Cancelled;

            if (b.Status == BookingStatus.Pending)
            {
                b.Status = BookingStatus.Cancelled;
                continue;
            }

            paidCount++;
            b.Status = BookingStatus.Refunded;
            var refund = await payments.RefundAsync(b.Id, 1.0m, b.CancelReason, actorUserId, ct);
            if (!refund.Succeeded)
            {
                refundFailed++;
                b.Status = BookingStatus.Cancelled;
                b.CancelReason += " — chưa hoàn được tiền, cần xử lý thủ công.";
            }
        }

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = "SHOWTIME_CANCELLED",
            EntityType = nameof(Showtime),
            EntityId = showtimeId.ToString(),
            Note = $"{reason} — hoàn tiền {paidCount - refundFailed}/{paidCount} đơn đã thanh toán"
                   + (refundFailed > 0 ? $", {refundFailed} đơn cần hoàn thủ công" : "")
        });

        await db.SaveChangesAsync(ct);
        return Result<int>.Ok(paidCount);
    }

    private static DateTime ToLocal(DateTime utc) => utc.AddHours(PricingService.VietnamUtcOffsetHours);
}
