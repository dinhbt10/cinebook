using CineBook.Api.Common;
using CineBook.Application.Dtos;
using CineBook.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Api.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController(
    BookingService bookings,
    // RefundService refunds,   // bật lại cùng endpoint /refund bên dưới
    TicketService tickets) : ControllerBase
{
    /// <summary>
    /// Giữ ghế và tạo đơn hàng ở trạng thái chờ thanh toán.
    /// Trả về SecondsLeft để giao diện chạy đồng hồ đếm ngược.
    /// </summary>
    [HttpPost("hold")]
    public async Task<IActionResult> Hold(HoldSeatsRequest req, CancellationToken ct) =>
        (await bookings.HoldSeatsAsync(User.UserId(), req, ct)).ToActionResult();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        // Chỉ chủ đơn được xem: đơn của người khác trả 404 như đơn không tồn tại.
        var booking = await bookings.GetForUserAsync(id, User.UserId(), ct);
        return booking is null ? NotFound(new { message = "Không tìm thấy đơn hàng." }) : Ok(booking);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] int take = 30, CancellationToken ct = default) =>
        Ok(await bookings.GetMyBookingsAsync(User.UserId(), take, ct));

    /// <summary>Khách thoát giữa luồng: nhả ghế ngay, không chờ hết 8 phút.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelPending(Guid id, CancellationToken ct) =>
        (await bookings.CancelPendingAsync(id, User.UserId(), ct)).ToActionResult();

    // BR-14: hủy vé đã thanh toán và hoàn tiền theo chính sách.
    // Tạm tắt: vé đã thanh toán thì khách không tự hủy được nữa. Bỏ comment để bật lại
    // luồng hủy vé + hoàn tiền theo BR-14 (RefundService vẫn giữ nguyên).
    // Rạp hủy suất chiếu vẫn hoàn 100% cho khách — luồng đó nằm ở ShowtimeService.CancelAsync.
    // [HttpPost("{id:guid}/refund")]
    // public async Task<IActionResult> Refund(Guid id, CancellationToken ct) =>
    //     (await refunds.CancelPaidBookingAsync(id, User.UserId(), ct)).ToActionResult();

    [HttpGet("{id:guid}/tickets")]
    public async Task<IActionResult> Tickets(Guid id, CancellationToken ct) =>
        Ok(await tickets.GetByBookingAsync(id, User.UserId(), ct));
}
