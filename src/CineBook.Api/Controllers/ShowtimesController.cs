using CineBook.Api.Common;
using CineBook.Application.Dtos;
using CineBook.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Api.Controllers;

[ApiController]
[Route("api/showtimes")]
public class ShowtimesController(ShowtimeService showtimes, BookingService bookings) : ControllerBase
{
    /// <summary>Lịch chiếu theo phim và/hoặc theo rạp, cho một ngày (giờ Việt Nam).</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] Guid? movieId, [FromQuery] Guid? cinemaId, [FromQuery] DateOnly? date,
        CancellationToken ct) =>
        Ok(await showtimes.SearchAsync(movieId, cinemaId, date, ct));

    /// <summary>
    /// Sơ đồ ghế của một suất. Frontend gọi lại mỗi 5 giây khi khách đang chọn ghế —
    /// bản đơn giản thay cho WebSocket (§9.2 đánh giá công nghệ).
    /// </summary>
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

    /// <summary>
    /// Lịch chiếu cho trang quản trị: gồm cả suất chưa mở bán, đã qua giờ và đã hủy.
    /// Quản lý rạp chỉ thấy rạp mình (BR-19).
    /// </summary>
    [HttpGet("admin")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> AdminList(
        [FromQuery] DateOnly date, [FromQuery] Guid? cinemaId, CancellationToken ct) =>
        Ok(await showtimes.SearchForAdminAsync(cinemaId, date, User.ManagedCinemaId(), ct));

    /// <summary>Giá gợi ý cho từng loại ghế theo bảng giá, để form tạo suất điền sẵn.</summary>
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

    /// <summary>BR-15: hủy suất chiếu, mọi vé đã bán được hoàn 100%.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelShowtimeRequest req, CancellationToken ct) =>
        (await showtimes.CancelAsync(id, req.Reason, User.ManagedCinemaId(), User.UserId(), ct)).ToActionResult();
}

public record CancelShowtimeRequest(string Reason);
