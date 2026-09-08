using CineBook.Application.Abstractions;
using CineBook.Application.Dtos;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Application.Services;

public class TicketService(IAppDbContext db, IQrSigner qrSigner)
{
    public async Task<IReadOnlyList<TicketDto>> GetByBookingAsync(
        Guid bookingId, Guid userId, CancellationToken ct = default)
    {
        return await db.Tickets.AsNoTracking()
            .Include(t => t.Seat)
            .Include(t => t.Booking).ThenInclude(b => b.Showtime).ThenInclude(s => s.Movie)
            .Include(t => t.Booking).ThenInclude(b => b.Showtime).ThenInclude(s => s.Room)
                .ThenInclude(r => r.Cinema)
            .Where(t => t.BookingId == bookingId && t.Booking.UserId == userId)
            .OrderBy(t => t.Seat.RowLabel).ThenBy(t => t.Seat.Number)
            .ToListAsync(ct)
            // Chữ ký QR tính ở phía ứng dụng, không dịch được sang SQL.
            .ContinueWith(task => (IReadOnlyList<TicketDto>)task.Result.Select(t => new TicketDto(
                t.Id, t.Code, qrSigner.BuildPayload(t.Code), t.Seat.Label, t.Status.ToString(),
                t.Booking.Showtime.Movie.Title, t.Booking.Showtime.Room.Cinema.Name,
                t.Booking.Showtime.Room.Name, t.Booking.Showtime.StartsAtUtc, t.CheckedInAtUtc)).ToList(), ct);
    }

    /// <summary>Nội dung email vé gửi cho khách sau khi thanh toán thành công.</summary>
    public static string BuildTicketEmail(string movieTitle, string cinemaName, string roomName,
        DateTime startsAtLocal, string bookingCode, IEnumerable<string> seatLabels, decimal total)
    {
        var seats = string.Join(", ", seatLabels);
        return $"""
            <div style="font-family:system-ui,sans-serif;max-width:520px">
              <h2 style="margin:0 0 4px">Vé của bạn đã sẵn sàng</h2>
              <p style="color:#555;margin:0 0 16px">Mã đặt vé <b>{bookingCode}</b></p>
              <table cellpadding="6" style="border-collapse:collapse;width:100%">
                <tr><td style="color:#777">Phim</td><td><b>{movieTitle}</b></td></tr>
                <tr><td style="color:#777">Rạp</td><td>{cinemaName} — {roomName}</td></tr>
                <tr><td style="color:#777">Suất chiếu</td><td>{startsAtLocal:HH:mm dd/MM/yyyy}</td></tr>
                <tr><td style="color:#777">Ghế</td><td><b>{seats}</b></td></tr>
                <tr><td style="color:#777">Tổng tiền</td><td><b>{total:N0}đ</b></td></tr>
              </table>
              <p style="color:#777;font-size:13px">Xuất trình mã QR trong mục "Vé của tôi" tại cửa phòng chiếu.</p>
            </div>
            """;
    }
}
