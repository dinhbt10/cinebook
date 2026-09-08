using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Application.Dtos;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CineBook.Application.Services;

/// <summary>
/// Soát vé tại cửa phòng chiếu — §06 tài liệu flow.
/// Sáu kết quả: vé giả, dùng lại, đã hủy, sai suất/sai rạp, ngoài giờ, cho vào.
/// </summary>
public class CheckInService(
    IAppDbContext db,
    IQrSigner qrSigner,
    IDateTimeProvider clock,
    IOptions<BookingOptions> options)
{
    private readonly BookingOptions _opt = options.Value;

    public async Task<CheckInResponse> ScanAsync(
        string qrPayload, Guid staffUserId, Guid? staffCinemaId, CancellationToken ct = default)
    {
        // BR-11: mã QR phải mang chữ ký hợp lệ.
        if (!qrSigner.TryParsePayload(qrPayload, out var ticketCode))
            return Deny("VE_GIA", "Mã QR không hợp lệ hoặc đã bị chỉnh sửa.");

        var ticket = await db.Tickets
            .Include(t => t.Seat)
            .Include(t => t.Booking).ThenInclude(b => b.Showtime).ThenInclude(s => s.Movie)
            .Include(t => t.Booking).ThenInclude(b => b.Showtime).ThenInclude(s => s.Room)
                .ThenInclude(r => r.Cinema)
            .FirstOrDefaultAsync(t => t.Code == ticketCode, ct);

        if (ticket is null) return Deny("VE_GIA", "Không tìm thấy vé.");

        var showtime = ticket.Booking.Showtime;
        var info = new { showtime.Movie.Title, Room = showtime.Room.Name, showtime.StartsAtUtc };

        // BR-12
        if (ticket.Status == TicketStatus.Used)
            return new CheckInResponse(false, "DUNG_LAI",
                $"Vé đã được sử dụng lúc {ToLocal(ticket.CheckedInAtUtc!.Value):HH:mm dd/MM}.",
                ticket.Code, ticket.Seat.Label, info.Title, info.Room, info.StartsAtUtc);

        // BR-13
        if (ticket.Status == TicketStatus.Cancelled
            || ticket.Booking.Status is BookingStatus.Cancelled or BookingStatus.Refunded)
            return new CheckInResponse(false, "DA_HUY", "Vé đã bị hủy hoặc đã hoàn tiền.",
                ticket.Code, ticket.Seat.Label, info.Title, info.Room, info.StartsAtUtc);

        if (ticket.Booking.Status != BookingStatus.Paid)
            return new CheckInResponse(false, "CHUA_THANH_TOAN", "Đơn hàng chưa thanh toán.",
                ticket.Code, ticket.Seat.Label, info.Title, info.Room, info.StartsAtUtc);

        // BR-19: nhân viên chỉ soát vé của rạp mình.
        if (staffCinemaId is { } cid && showtime.Room.CinemaId != cid)
            return new CheckInResponse(false, "SAI_RAP",
                $"Vé này thuộc rạp {showtime.Room.Cinema.Name}.",
                ticket.Code, ticket.Seat.Label, info.Title, info.Room, info.StartsAtUtc);

        var now = clock.UtcNow;
        if (now < showtime.StartsAtUtc.AddMinutes(-_opt.CheckInOpenBeforeMinutes))
            return new CheckInResponse(false, "QUA_SOM",
                $"Chưa tới giờ vào cửa. Mở cửa từ {ToLocal(showtime.StartsAtUtc.AddMinutes(-_opt.CheckInOpenBeforeMinutes)):HH:mm}.",
                ticket.Code, ticket.Seat.Label, info.Title, info.Room, info.StartsAtUtc);

        if (now > showtime.StartsAtUtc.AddMinutes(_opt.CheckInCloseAfterMinutes))
            return new CheckInResponse(false, "QUA_MUON",
                "Đã quá giờ vào cửa, cần nhân viên duyệt thủ công.",
                ticket.Code, ticket.Seat.Label, info.Title, info.Room, info.StartsAtUtc);

        // BR-12: đổi trạng thái nguyên tử — hai máy quét cùng lúc thì chỉ một máy thành công.
        var affected = await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Tickets"
            SET "Status" = {(int)TicketStatus.Used},
                "CheckedInAtUtc" = {now},
                "CheckedInByUserId" = {staffUserId}
            WHERE "Id" = {ticket.Id}
              AND "Status" = {(int)TicketStatus.Valid}
            """, ct);

        if (affected != 1)
            return new CheckInResponse(false, "DUNG_LAI",
                "Vé vừa được quét ở một máy khác.",
                ticket.Code, ticket.Seat.Label, info.Title, info.Room, info.StartsAtUtc);

        return new CheckInResponse(true, "CHO_VAO",
            $"Mời khách vào {info.Room}, ghế {ticket.Seat.Label}.",
            ticket.Code, ticket.Seat.Label, info.Title, info.Room, info.StartsAtUtc);
    }

    /// <summary>Tra cứu bằng mã vé khi mã QR mờ không quét được.</summary>
    public async Task<CheckInResponse> ScanByCodeAsync(
        string ticketCode, Guid staffUserId, Guid? staffCinemaId, CancellationToken ct = default)
        => await ScanAsync(qrSigner.BuildPayload(ticketCode.Trim().ToUpperInvariant()),
                           staffUserId, staffCinemaId, ct);

    private static CheckInResponse Deny(string verdict, string message) =>
        new(false, verdict, message, null, null, null, null, null);

    private static DateTime ToLocal(DateTime utc) => utc.AddHours(PricingService.VietnamUtcOffsetHours);
}
