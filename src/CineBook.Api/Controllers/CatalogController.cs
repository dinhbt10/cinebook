using CineBook.Api.Common;
using CineBook.Application.Abstractions;
using CineBook.Application.Dtos;
using CineBook.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Api.Controllers;

/// <summary>Rạp, phòng chiếu, bắp nước và khuyến mãi.</summary>
[ApiController]
[Route("api")]
public class CatalogController(
    IAppDbContext db,
    RoomService rooms,
    PromotionService promotions) : ControllerBase
{
    [HttpGet("cinemas")]
    public async Task<IActionResult> Cinemas([FromQuery] string? city, CancellationToken ct)
    {
        var q = db.Cinemas.AsNoTracking().Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(c => c.City == city);

        return Ok(await q.OrderBy(c => c.City).ThenBy(c => c.Name)
            .Select(c => new CinemaDto(c.Id, c.Name, c.City, c.District, c.Address, c.Hotline))
            .ToListAsync(ct));
    }

    [HttpGet("cinemas/cities")]
    public async Task<IActionResult> Cities(CancellationToken ct) =>
        Ok(await db.Cinemas.AsNoTracking().Where(c => c.IsActive)
            .Select(c => c.City).Distinct().OrderBy(c => c).ToListAsync(ct));

    [HttpGet("cinemas/{id:guid}/rooms")]
    public async Task<IActionResult> Rooms(Guid id, CancellationToken ct) =>
        Ok(await rooms.GetByCinemaAsync(id, ct));

    [HttpPost("cinemas")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> CreateCinema(SaveCinemaRequest req, CancellationToken ct)
    {
        var cinema = new Domain.Entities.Cinema
        {
            Name = req.Name.Trim(), City = req.City.Trim(), District = req.District,
            Address = req.Address.Trim(), Hotline = req.Hotline
        };
        db.Cinemas.Add(cinema);
        await db.SaveChangesAsync(ct);
        return Ok(new { cinema.Id });
    }

    [HttpPost("rooms")]
    [Authorize(Roles = Roles.AdminOrManager)]
    public async Task<IActionResult> CreateRoom(CreateRoomRequest req, CancellationToken ct) =>
        (await rooms.CreateAsync(req, User.ManagedCinemaId(), ct)).ToActionResult();

    /// <summary>BR-06: nhân viên báo ghế hỏng ngay tại rạp.</summary>
    [HttpPatch("seats/{seatId:guid}/maintenance")]
    [Authorize(Roles = Roles.StaffOrManager)]
    public async Task<IActionResult> SetSeatMaintenance(
        Guid seatId, [FromQuery] bool broken, CancellationToken ct) =>
        (await rooms.SetSeatMaintenanceAsync(seatId, broken, User.ManagedCinemaId(), ct)).ToActionResult();

    [HttpGet("combos")]
    public async Task<IActionResult> Combos(CancellationToken ct) =>
        Ok(await db.Combos.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Price)
            .Select(c => new ComboDto(c.Id, c.Name, c.Description, c.Price, c.ImageUrl))
            .ToListAsync(ct));

    // ---------------------------------------------------------------- voucher

    /// <summary>Voucher đang chạy, ai xem cũng được — dùng cho trang Ưu đãi.</summary>
    [HttpGet("promotions")]
    public async Task<IActionResult> Promotions(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        return Ok(await db.Promotions.AsNoTracking()
            .Where(p => p.IsActive && p.StartsAtUtc <= now && p.EndsAtUtc >= now)
            .OrderBy(p => p.EndsAtUtc)
            .Select(p => new PromotionDto(p.Id, p.Code, p.Name, p.DiscountType.ToString(),
                p.Value, p.MaxDiscount, p.MinOrderAmount, p.EndsAtUtc))
            .ToListAsync(ct));
    }

    /// <summary>
    /// Voucher cho một đơn hàng cụ thể: mỗi cái kèm số tiền giảm thật và lý do nếu
    /// chưa dùng được. Truyền amount = tạm tính của đơn (tiền ghế + bắp nước).
    /// Đặt saved=true để chỉ lấy voucher trong kho.
    /// </summary>
    [HttpGet("promotions/for-order")]
    [Authorize]
    public async Task<IActionResult> ForOrder(
        [FromQuery] decimal amount, [FromQuery] bool saved = false, CancellationToken ct = default) =>
        Ok(await promotions.GetForOrderAsync(User.UserId(), amount, saved, ct));

    /// <summary>Kho voucher của khách.</summary>
    [HttpGet("promotions/mine")]
    [Authorize]
    public async Task<IActionResult> MyVouchers([FromQuery] decimal amount = 0, CancellationToken ct = default) =>
        Ok(await promotions.GetForOrderAsync(User.UserId(), amount, savedOnly: true, ct));

    [HttpPost("promotions/{id:guid}/save")]
    [Authorize]
    public async Task<IActionResult> SaveVoucher(Guid id, CancellationToken ct) =>
        (await promotions.SaveAsync(User.UserId(), id, ct)).ToActionResult();

    /// <summary>Lưu bằng mã, cho ô "nhập mã" ở trang Ưu đãi.</summary>
    [HttpPost("promotions/save-by-code")]
    [Authorize]
    public async Task<IActionResult> SaveByCode(SaveVoucherRequest req, CancellationToken ct) =>
        (await promotions.SaveByCodeAsync(User.UserId(), req.Code, ct)).ToActionResult();

    [HttpDelete("promotions/{id:guid}/save")]
    [Authorize]
    public async Task<IActionResult> RemoveVoucher(Guid id, CancellationToken ct) =>
        (await promotions.RemoveAsync(User.UserId(), id, ct)).ToActionResult();

    // ---------------------------------------------------------------- quản trị voucher
    // Voucher áp cho toàn hệ thống nên chỉ quản trị viên được quản lý; quản lý rạp
    // chỉ phụ trách một rạp (BR-19).

    /// <summary>Mọi voucher kể cả đã tắt hay hết hạn — khác /promotions chỉ trả voucher đang chạy.</summary>
    [HttpGet("promotions/all")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> AllPromotions(CancellationToken ct) =>
        Ok(await promotions.GetAllForAdminAsync(ct));

    [HttpPost("promotions")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> CreatePromotion(SavePromotionRequest req, CancellationToken ct) =>
        (await promotions.CreateAsync(req, ct)).ToActionResult();

    [HttpPut("promotions/{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> UpdatePromotion(Guid id, SavePromotionRequest req, CancellationToken ct) =>
        (await promotions.UpdateAsync(id, req, ct)).ToActionResult();

    [HttpPatch("promotions/{id:guid}/active")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> SetPromotionActive(Guid id, [FromQuery] bool value, CancellationToken ct) =>
        (await promotions.SetActiveAsync(id, value, ct)).ToActionResult();

    [HttpDelete("promotions/{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DeletePromotion(Guid id, CancellationToken ct)
    {
        var result = await promotions.DeleteAsync(id, ct);
        if (!result.Succeeded) return result.ToActionResult();

        return result.Value
            ? NoContent()
            : Ok(new { message = "Voucher đã có đơn sử dụng nên được chuyển sang ngừng áp dụng thay vì xoá." });
    }
}

public record SaveVoucherRequest(string Code);
