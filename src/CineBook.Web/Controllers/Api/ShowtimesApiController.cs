using CineBook.Web.Common;
using CineBook.Application.Dtos;
using CineBook.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Web.Controllers.Api;

[ApiController]
[Route("api/showtimes")]
public class ShowtimesApiController(ShowtimeService showtimes, BookingService bookings) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? movieId, [FromQuery] Guid? cinemaId, [FromQuery] DateOnly? date,
        CancellationToken ct) =>
        Ok(await showtimes.SearchAsync(movieId, cinemaId, date, ct));

    [HttpGet("{id:guid}/seatmap")]
    public async Task<IActionResult> SeatMap(Guid id, CancellationToken ct)
    {
        Guid? userId = User.Identity?.IsAuthenticated == true ? User.UserId() : null;
        return (await bookings.GetSeatMapAsync(id, userId, ct)).ToActionResult();
    }

    [HttpPost]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Create(CreateShowtimeRequest req, CancellationToken ct) =>
        (await showtimes.CreateAsync(req, User.ManagedCinemaId(), ct)).ToActionResult();

    [HttpGet("admin")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> AdminList(
        [FromQuery] DateOnly date, [FromQuery] Guid? cinemaId, CancellationToken ct) =>
        Ok(await showtimes.SearchForAdminAsync(cinemaId, date, User.ManagedCinemaId(), ct));

    [HttpGet("price-preview")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> PricePreview(
        [FromQuery] Guid roomId, [FromQuery] DateTime startsAtUtc, [FromQuery] decimal basePrice,
        CancellationToken ct) =>
        (await showtimes.PreviewPricesAsync(roomId, startsAtUtc, basePrice, ct)).ToActionResult();

    [HttpPatch("{id:guid}/on-sale")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> SetOnSale(Guid id, [FromQuery] bool value, CancellationToken ct) =>
        (await showtimes.SetOnSaleAsync(id, value, User.ManagedCinemaId(), ct)).ToActionResult();

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelShowtimeRequest req, CancellationToken ct) =>
        (await showtimes.CancelAsync(id, req.Reason, User.ManagedCinemaId(), User.UserId(), ct)).ToActionResult();
}

public record CancelShowtimeRequest(string Reason);
