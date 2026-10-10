using CineBook.Web.Common;
using CineBook.Application.Dtos;
using CineBook.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Web.Controllers.Api;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsApiController(
    BookingService bookings,
    TicketService tickets) : ControllerBase
{
    [HttpPost("hold")]
    public async Task<IActionResult> Hold(HoldSeatsRequest req, CancellationToken ct) =>
        (await bookings.HoldSeatsAsync(User.UserId(), req, ct)).ToActionResult();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var booking = await bookings.GetForUserAsync(id, User.UserId(), ct);
        return booking is null ? NotFound(new { message = "Không tìm thấy đơn hàng." }) : Ok(booking);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] int take = 30, CancellationToken ct = default) =>
        Ok(await bookings.GetMyBookingsAsync(User.UserId(), take, ct));

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelPending(Guid id, CancellationToken ct) =>
        (await bookings.CancelPendingAsync(id, User.UserId(), ct)).ToActionResult();

    [HttpGet("{id:guid}/tickets")]
    public async Task<IActionResult> Tickets(Guid id, CancellationToken ct) =>
        Ok(await tickets.GetByBookingAsync(id, User.UserId(), ct));
}
