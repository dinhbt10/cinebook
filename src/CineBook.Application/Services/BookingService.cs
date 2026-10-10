using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Application.Dtos;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CineBook.Application.Services;

public class BookingService(
    IAppDbContext db,
    IDateTimeProvider clock,
    IQrSigner qrSigner,
    PromotionService promotions,
    IOptions<BookingOptions> options,
    ILogger<BookingService> logger)
{
    private readonly BookingOptions _opt = options.Value;

    public async Task<Result<SeatMapDto>> GetSeatMapAsync(
        Guid showtimeId, Guid? currentUserId, CancellationToken ct = default)
    {
        var showtime = await db.Showtimes.AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Room).ThenInclude(r => r.Cinema)
            .FirstOrDefaultAsync(s => s.Id == showtimeId, ct);

        if (showtime is null)
            return Result<SeatMapDto>.Fail("NOT_FOUND", "Không tìm thấy suất chiếu.");

        var now = clock.UtcNow;
        var rows = await db.ShowtimeSeats.AsNoTracking()
            .Where(x => x.ShowtimeId == showtimeId)
            .Include(x => x.Seat)
            .OrderBy(x => x.Seat.RowLabel).ThenBy(x => x.Seat.Number)
            .ToListAsync(ct);

        var seats = rows.Select(x => new SeatMapSeatDto(
            x.SeatId,
            x.Seat.RowLabel,
            x.Seat.Number,
            x.Seat.Label,
            x.Seat.SeatType.ToString(),
            Display(x, now).ToString(),
            x.Price,
            x.Status == SeatStatus.Held && x.HoldExpiresAtUtc > now && x.HeldByUserId == currentUserId));

        return Result<SeatMapDto>.Ok(new SeatMapDto(
            showtime.Id, showtime.Movie.Title, showtime.Room.Cinema.Name, showtime.Room.Name,
            showtime.StartsAtUtc, _opt.MaxSeatsPerBooking, seats));
    }

    private static SeatStatus Display(ShowtimeSeat s, DateTime now) =>
        s.Status == SeatStatus.Held && s.HoldExpiresAtUtc <= now ? SeatStatus.Available : s.Status;

    public async Task<Result<BookingDto>> HoldSeatsAsync(
        Guid userId, HoldSeatsRequest req, CancellationToken ct = default)
    {
        var seatIds = req.SeatIds.Distinct().ToArray();

        if (seatIds.Length == 0)
            return Result<BookingDto>.Fail("NO_SEAT", "Chưa chọn ghế nào.");

        if (seatIds.Length > _opt.MaxSeatsPerBooking)
            return Result<BookingDto>.Fail("TOO_MANY_SEATS",
                $"Mỗi lần đặt tối đa {_opt.MaxSeatsPerBooking} ghế.");

        var showtime = await db.Showtimes.AsNoTracking()
            .Include(s => s.Movie)
            .Include(s => s.Room).ThenInclude(r => r.Cinema)
            .FirstOrDefaultAsync(s => s.Id == req.ShowtimeId, ct);

        if (showtime is null)
            return Result<BookingDto>.Fail("SHOWTIME_NOT_FOUND", "Không tìm thấy suất chiếu.");
        if (showtime.IsCancelled)
            return Result<BookingDto>.Fail("SHOWTIME_CANCELLED", "Suất chiếu đã bị hủy.");
        if (!showtime.IsOnSale)
            return Result<BookingDto>.Fail("NOT_ON_SALE", "Suất chiếu chưa mở bán vé.");

        var now = clock.UtcNow;

        if (showtime.StartsAtUtc <= now.AddMinutes(_opt.MinutesBeforeStartToBlock))
            return Result<BookingDto>.Fail("TOO_LATE",
                $"Suất chiếu sắp bắt đầu, ngừng bán trước giờ chiếu {_opt.MinutesBeforeStartToBlock} phút.");

        var holdExpires = now.AddMinutes(_opt.HoldMinutes);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        await CloseOwnPendingAsync(userId, showtime.Id, now, ct);

        var booking = new Booking
        {
            Code = CodeGenerator.New("CB"),
            UserId = userId,
            ShowtimeId = showtime.Id,
            Status = BookingStatus.Pending,
            HoldExpiresAtUtc = holdExpires,
            CreatedAtUtc = now
        };
        db.Bookings.Add(booking);
        await db.SaveChangesAsync(ct);

        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ShowtimeSeats"
            SET "Status" = {(int)SeatStatus.Held},
                "HeldByUserId" = {userId},
                "HoldExpiresAtUtc" = {holdExpires},
                "BookingId" = {booking.Id}
            WHERE "ShowtimeId" = {req.ShowtimeId}
              AND "SeatId" = ANY({seatIds})
              AND ( "Status" = {(int)SeatStatus.Available}
                    OR ( "Status" = {(int)SeatStatus.Held} AND "HoldExpiresAtUtc" < {now} ) )
            """, ct);

        if (affected != seatIds.Length)
        {
            await tx.RollbackAsync(ct);
            logger.LogInformation(
                "Giành ghế thất bại cho suất {ShowtimeId}: yêu cầu {Requested}, giành được {Affected}",
                req.ShowtimeId, seatIds.Length, affected);
            return Result<BookingDto>.Fail("SEATS_TAKEN",
                "Một số ghế vừa có người khác chọn. Vui lòng làm mới sơ đồ ghế và chọn lại.");
        }

        var heldSeats = await db.ShowtimeSeats
            .Include(x => x.Seat)
            .Where(x => x.BookingId == booking.Id)
            .ToListAsync(ct);

        booking.SeatsSubtotal = heldSeats.Sum(x => x.Price);

        var comboResult = await AddCombosAsync(booking, req.Combos, ct);
        if (!comboResult.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return Result<BookingDto>.Fail(comboResult.ErrorCode!, comboResult.Error!);
        }

        var discountResult = await ApplyPromotionAsync(booking, userId, req.PromotionCode, now, ct);
        if (!discountResult.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return Result<BookingDto>.Fail(discountResult.ErrorCode!, discountResult.Error!);
        }

        booking.Total = booking.SeatsSubtotal + booking.ConcessionSubtotal - booking.DiscountAmount;
        if (booking.Total < 0) booking.Total = 0;

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Result<BookingDto>.Ok(await MapAsync(booking.Id, ct));
    }

    private async Task CloseOwnPendingAsync(Guid userId, Guid showtimeId, DateTime now, CancellationToken ct)
    {
        var stale = await db.Bookings
            .Where(b => b.UserId == userId && b.ShowtimeId == showtimeId && b.Status == BookingStatus.Pending)
            .ToListAsync(ct);

        foreach (var b in stale)
        {
            if (b.HoldExpiresAtUtc <= now)
            {
                b.Status = BookingStatus.Expired;
            }
            else
            {
                b.Status = BookingStatus.Cancelled;
                b.CancelledAtUtc = now;
                b.CancelReason = "Khách chọn lại ghế";
            }
            b.UpdatedAtUtc = now;
            await ReleaseSeatsAsync(b.Id, ct);
        }
    }

    private async Task<Result> AddCombosAsync(
        Booking booking, IReadOnlyList<ComboSelection>? selections, CancellationToken ct)
    {
        booking.ConcessionSubtotal = 0m;
        if (selections is null || selections.Count == 0) return Result.Ok();

        var ids = selections.Select(c => c.ComboId).Distinct().ToList();
        var combos = await db.Combos.Where(c => ids.Contains(c.Id) && c.IsActive).ToListAsync(ct);

        if (combos.Count != ids.Count)
            return Result.Fail("COMBO_NOT_FOUND", "Có combo không còn được bán.");

        foreach (var sel in selections)
        {
            if (sel.Quantity <= 0) continue;
            var combo = combos.First(c => c.Id == sel.ComboId);
            db.BookingCombos.Add(new BookingCombo
            {
                BookingId = booking.Id,
                ComboId = combo.Id,
                Quantity = sel.Quantity,
                UnitPrice = combo.Price
            });
            booking.ConcessionSubtotal += combo.Price * sel.Quantity;
        }
        return Result.Ok();
    }

    private async Task<Result> ApplyPromotionAsync(
        Booking booking, Guid userId, string? code, DateTime now, CancellationToken ct)
    {
        booking.DiscountAmount = 0m;
        booking.PromotionId = null;
        if (string.IsNullOrWhiteSpace(code)) return Result.Ok();

        var promo = await db.Promotions
            .FirstOrDefaultAsync(p => p.Code == code.Trim().ToUpperInvariant(), ct);

        if (promo is null) return Result.Fail("PROMO_INVALID", "Mã giảm giá không tồn tại.");

        var orderValue = booking.SeatsSubtotal + booking.ConcessionSubtotal;
        var check = await promotions.EvaluateAsync(promo, userId, orderValue, ct);

        if (!check.Usable) return Result.Fail(check.ErrorCode!, check.Reason!);

        booking.PromotionId = promo.Id;
        booking.DiscountAmount = check.Discount;
        return Result.Ok();
    }

    public async Task<Result> CancelPendingAsync(Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId, ct);
        if (booking is null) return Result.Fail("NOT_FOUND", "Không tìm thấy đơn hàng.");
        if (booking.Status != BookingStatus.Pending)
            return Result.Fail("INVALID_STATE", "Đơn hàng không ở trạng thái chờ thanh toán.");

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAtUtc = clock.UtcNow;
        booking.CancelReason = "Khách hủy trong luồng đặt vé";
        await ReleaseSeatsAsync(booking.Id, ct);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result<Booking>> ConfirmPaidAsync(Guid bookingId, CancellationToken ct = default)
    {
        var booking = await db.Bookings
            .Include(b => b.Showtime)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking is null) return Result<Booking>.Fail("NOT_FOUND", "Không tìm thấy đơn hàng.");
        if (booking.Status == BookingStatus.Paid) return Result<Booking>.Ok(booking);
        if (booking.Status != BookingStatus.Pending)
            return Result<Booking>.Fail("INVALID_STATE",
                $"Đơn hàng đang ở trạng thái {booking.Status}, không thể ghi nhận thanh toán.");

        var now = clock.UtcNow;

        var sold = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ShowtimeSeats"
            SET "Status" = {(int)SeatStatus.Sold},
                "HoldExpiresAtUtc" = NULL
            WHERE "BookingId" = {bookingId}
              AND "Status" = {(int)SeatStatus.Held}
            """, ct);

        if (sold == 0)
            return Result<Booking>.Fail("SEATS_LOST",
                "Ghế đã được nhả trước khi thanh toán kịp ghi nhận. Cần hoàn tiền cho khách.");

        booking.Status = BookingStatus.Paid;
        booking.PaidAtUtc = now;

        var seats = await db.ShowtimeSeats.Include(x => x.Seat)
            .Where(x => x.BookingId == bookingId).ToListAsync(ct);

        foreach (var s in seats)
        {
            var code = CodeGenerator.New("TK");
            db.Tickets.Add(new Ticket
            {
                BookingId = booking.Id,
                SeatId = s.SeatId,
                Code = code,
                QrSignature = qrSigner.Sign(code),
                Status = TicketStatus.Valid
            });
        }

        if (booking.PromotionId is { } promoId)
        {
            var promo = await db.Promotions.FirstOrDefaultAsync(p => p.Id == promoId, ct);
            if (promo is not null) promo.UsedCount += 1;
        }

        await db.SaveChangesAsync(ct);
        return Result<Booking>.Ok(booking);
    }

    public async Task<int> ExpireOverdueBookingsAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        var released = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ShowtimeSeats"
            SET "Status" = {(int)SeatStatus.Available},
                "HeldByUserId" = NULL,
                "HoldExpiresAtUtc" = NULL,
                "BookingId" = NULL
            WHERE "Status" = {(int)SeatStatus.Held}
              AND "HoldExpiresAtUtc" < {now}
            """, ct);

        var expired = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Bookings"
            SET "Status" = {(int)BookingStatus.Expired},
                "UpdatedAtUtc" = {now}
            WHERE "Status" = {(int)BookingStatus.Pending}
              AND "HoldExpiresAtUtc" < {now}
            """, ct);

        if (expired > 0)
            logger.LogInformation("Dọn {Expired} đơn quá hạn, nhả {Released} ghế", expired, released);

        return expired;
    }

    private Task ReleaseSeatsAsync(Guid bookingId, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "ShowtimeSeats"
            SET "Status" = {(int)SeatStatus.Available},
                "HeldByUserId" = NULL,
                "HoldExpiresAtUtc" = NULL,
                "BookingId" = NULL
            WHERE "BookingId" = {bookingId}
              AND "Status" <> {(int)SeatStatus.Maintenance}
            """, ct);

    public Task ReleaseSeatsOfBookingAsync(Guid bookingId, CancellationToken ct = default) =>
        ReleaseSeatsAsync(bookingId, ct);

    public async Task<BookingDto> MapAsync(Guid bookingId, CancellationToken ct = default)
    {
        var b = await BookingsWithDetails()
            .FirstAsync(x => x.Id == bookingId, ct);
        return (await ToDtosAsync([b], ct))[0];
    }

    public async Task<BookingDto?> GetForUserAsync(Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        var b = await BookingsWithDetails()
            .FirstOrDefaultAsync(x => x.Id == bookingId && x.UserId == userId, ct);
        return b is null ? null : (await ToDtosAsync([b], ct))[0];
    }

    public async Task<IReadOnlyList<BookingDto>> GetMyBookingsAsync(
        Guid userId, int take = 30, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var bookings = await BookingsWithDetails()
            .Where(b => b.UserId == userId && b.Status != BookingStatus.Expired)
            .Where(b => b.Status != BookingStatus.Pending || b.HoldExpiresAtUtc > now)
            .Where(b => b.Status != BookingStatus.Cancelled || b.PaidAtUtc != null)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(ct);

        return await ToDtosAsync(bookings, ct);
    }

    private IQueryable<Booking> BookingsWithDetails() =>
        db.Bookings.AsNoTracking()
            .Include(x => x.Showtime).ThenInclude(s => s.Movie)
            .Include(x => x.Showtime).ThenInclude(s => s.Room).ThenInclude(r => r.Cinema)
            .Include(x => x.BookedSeats).ThenInclude(s => s.Seat)
            .Include(x => x.Combos).ThenInclude(c => c.Combo)
            .Include(x => x.Tickets).ThenInclude(t => t.Seat)
            .AsSplitQuery();

    private async Task<IReadOnlyList<BookingDto>> ToDtosAsync(
        IReadOnlyList<Booking> bookings, CancellationToken ct)
    {
        var fromTickets = bookings.Where(b => b.BookedSeats.Count == 0 && b.Tickets.Count > 0).ToList();
        var prices = new Dictionary<(Guid, Guid), decimal>();
        if (fromTickets.Count > 0)
        {
            var showtimeIds = fromTickets.Select(b => b.ShowtimeId).Distinct().ToList();
            var seatIds = fromTickets.SelectMany(b => b.Tickets.Select(t => t.SeatId)).Distinct().ToList();
            prices = await db.ShowtimeSeats.AsNoTracking()
                .Where(x => showtimeIds.Contains(x.ShowtimeId) && seatIds.Contains(x.SeatId))
                .ToDictionaryAsync(x => (x.ShowtimeId, x.SeatId), x => x.Price, ct);
        }

        return bookings.Select(b => ToDto(b, prices)).ToList();
    }

    private BookingDto ToDto(Booking b, IReadOnlyDictionary<(Guid, Guid), decimal> ticketSeatPrices)
    {
        var secondsLeft = b.Status == BookingStatus.Pending
            ? Math.Max(0, (int)(b.HoldExpiresAtUtc - clock.UtcNow).TotalSeconds)
            : 0;

        var status = b.Status == BookingStatus.Pending && secondsLeft == 0
            ? BookingStatus.Expired
            : b.Status;

        var seats = b.BookedSeats.Count > 0
            ? b.BookedSeats.Select(s => (s.Seat, s.Price))
            : b.Tickets.Select(t => (t.Seat, Price: ticketSeatPrices.GetValueOrDefault((b.ShowtimeId, t.SeatId))));

        return new BookingDto(
            b.Id, b.Code, status.ToString(), b.ShowtimeId,
            b.Showtime.Movie.Title, b.Showtime.Room.Cinema.Name, b.Showtime.Room.Name,
            b.Showtime.StartsAtUtc,
            seats.OrderBy(s => s.Seat.RowLabel).ThenBy(s => s.Seat.Number)
                .Select(s => new BookingSeatDto(s.Seat.Id, s.Seat.Label, s.Seat.SeatType.ToString(), s.Price))
                .ToList(),
            b.Combos.Select(c => new BookingComboDto(c.ComboId, c.Combo.Name, c.Quantity, c.UnitPrice)).ToList(),
            b.SeatsSubtotal, b.ConcessionSubtotal, b.DiscountAmount, b.Total,
            b.HoldExpiresAtUtc, secondsLeft, b.PaidAtUtc, b.CancelReason);
    }
}
