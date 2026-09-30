using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CineBook.Application.Services;

/// <summary>Luồng hủy vé của khách hàng — §07 tài liệu flow.</summary>
public class RefundService(
    IAppDbContext db,
    BookingService bookings,
    PaymentService payments,
    IDateTimeProvider clock,
    IOptions<BookingOptions> options)
{
    private readonly BookingOptions _opt = options.Value;

    public async Task<Result<decimal>> CancelPaidBookingAsync(
        Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        var booking = await db.Bookings
            .Include(b => b.Showtime)
            .Include(b => b.Tickets)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId, ct);

        if (booking is null) return Result<decimal>.Fail("NOT_FOUND", "Không tìm thấy đơn hàng.");
        if (booking.Status != BookingStatus.Paid)
            return Result<decimal>.Fail("INVALID_STATE", "Chỉ hủy được đơn đã thanh toán.");
        if (booking.Tickets.Any(t => t.Status == TicketStatus.Used))
            return Result<decimal>.Fail("ALREADY_USED", "Vé đã được sử dụng, không thể hủy.");

        var hoursLeft = (booking.Showtime.StartsAtUtc - clock.UtcNow).TotalHours;

        // BR-14
        var rate = hoursLeft switch
        {
            var h when h >= _opt.RefundFullBeforeHours => 1.0m,
            var h when h >= _opt.RefundPartialBeforeHours => _opt.RefundPartialRate,
            _ => 0m
        };

        if (rate == 0m)
            return Result<decimal>.Fail("TOO_LATE",
                $"Chỉ được hủy vé trước giờ chiếu ít nhất {_opt.RefundPartialBeforeHours} giờ.");

        // Hủy vé và hoàn tiền là MỘT việc: cổng từ chối hoàn thì mọi thay đổi phải lùi lại,
        // nếu không khách mất vé mà không nhận lại tiền.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        booking.Status = BookingStatus.Refunded;
        booking.CancelledAtUtc = clock.UtcNow;
        booking.CancelReason = "Khách hủy vé";
        foreach (var t in booking.Tickets) t.Status = TicketStatus.Cancelled;

        // Trả ghế lên sàn ngay để người khác mua được.
        await bookings.ReleaseSeatsOfBookingAsync(booking.Id, ct);

        var refund = await payments.RefundAsync(booking.Id, rate, "Khách hủy vé", userId, ct);
        if (!refund.Succeeded)
        {
            await tx.RollbackAsync(ct);
            return refund;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return Result<decimal>.Ok(refund.Value);
    }
}
