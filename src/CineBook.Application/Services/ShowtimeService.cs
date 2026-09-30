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

    /// <summary>
    /// Tạo suất chiếu. Áp BR-16 (không chồng giờ trong cùng phòng),
    /// BR-17 (tự tính giờ kết thúc) và sinh sẵn một dòng ShowtimeSeat cho mỗi ghế
    /// với giá chốt luôn: giá quản trị nhập cho từng loại ghế, hoặc tính theo BR-18
    /// nếu không nhập.
    /// </summary>
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

        // BR-19: quản lý rạp chỉ xếp lịch cho phòng của rạp mình.
        if (scopeCinemaId is { } scope && room.CinemaId != scope)
            return Result<Guid>.Fail(ForbiddenCinema, ForbiddenCinemaMessage);

        if (req.StartsAtUtc <= clock.UtcNow)
            return Result<Guid>.Fail("START_IN_PAST", "Giờ chiếu phải ở tương lai.");

        if (req.SeatPrices is { } p && (p.Standard <= 0 || p.Vip <= 0 || p.Couple <= 0))
            return Result<Guid>.Fail("BAD_PRICE", "Giá vé của mọi loại ghế phải lớn hơn 0.");

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

    /// <summary>
    /// Giá từng loại ghế tính theo bảng giá (BR-18) cho một phòng và giờ chiếu, để form
    /// tạo suất điền sẵn. Quản trị xem được giá thật khách sẽ trả và sửa lại trước khi lưu.
    /// </summary>
    public async Task<Result<SeatTypePrices>> PreviewPricesAsync(
        Guid roomId, DateTime startsAtUtc, decimal basePrice, CancellationToken ct = default)
    {
        var room = await db.Rooms.AsNoTracking().FirstOrDefaultAsync(r => r.Id == roomId, ct);
        if (room is null) return Result<SeatTypePrices>.Fail("ROOM_NOT_FOUND", "Không tìm thấy phòng chiếu.");

        // Tham số query có hậu tố Z được ASP.NET đọc thành giờ máy chủ (Kind = Local),
        // phải đổi về UTC trước khi cộng 7 tiếng ra giờ Việt Nam.
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
                s.IsOnSale,
                s.IsCancelled))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Lịch chiếu cho trang quản trị: khác <see cref="SearchAsync"/> ở chỗ gồm cả suất chưa
    /// mở bán, đã qua giờ và đã hủy — quản trị phải thấy suất vừa bấm "ngừng bán" để mở lại.
    /// </summary>
    public async Task<IReadOnlyList<ShowtimeListItemDto>> SearchForAdminAsync(
        Guid? cinemaId, DateOnly localDate, Guid? scopeCinemaId = null, CancellationToken ct = default)
    {
        var fromUtc = PricingService.LocalDateStartUtc(localDate);
        var toUtc = fromUtc.AddDays(1);
        var nowUtc = clock.UtcNow;

        var q = db.Showtimes.AsNoTracking()
            .Where(s => s.StartsAtUtc >= fromUtc && s.StartsAtUtc < toUtc);

        // BR-19: quản lý rạp chỉ thấy rạp mình, bỏ qua rạp chọn trên giao diện.
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

    /// <summary>BR-15: hủy suất chiếu thì mọi vé đã bán được hoàn 100%.</summary>
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
                // Đơn chưa trả tiền: chỉ đóng lại. Nếu tiền về sau đó, luồng webhook tự hoàn.
                b.Status = BookingStatus.Cancelled;
                continue;
            }

            // BR-15: vé đã bán được hoàn 100%, không áp chính sách giờ hủy của khách.
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
