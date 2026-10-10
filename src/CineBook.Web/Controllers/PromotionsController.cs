using CineBook.Application.Dtos;
using CineBook.Application.Services;
using CineBook.Infrastructure.Persistence;
using CineBook.Web.Common;
using CineBook.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Web.Controllers;

public class PromotionsController : Controller
{
    private readonly CineBookDbContext _context;
    private readonly PromotionService _promotions;

    public PromotionsController(CineBookDbContext context, PromotionService promotions)
    {
        _context = context;
        _promotions = promotions;
    }

    public async Task<IActionResult> Index()
    {
        return View(await GetListAsync());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Guid id)
    {
        var result = await _promotions.SaveAsync(User.UserId(), id);
        if (!result.Succeeded)
            return BadRequest(new { message = result.Error });

        return PartialView("_VoucherList", await GetListAsync());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unsave(Guid id)
    {
        var result = await _promotions.RemoveAsync(User.UserId(), id);
        if (!result.Succeeded)
            return BadRequest(new { message = result.Error });

        return PartialView("_VoucherList", await GetListAsync());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { message = "Vui lòng nhập mã voucher." });

        var result = await _promotions.SaveByCodeAsync(User.UserId(), code);
        if (!result.Succeeded)
            return BadRequest(new { message = result.Error });

        return PartialView("_VoucherList", await GetListAsync());
    }

    private async Task<VoucherListViewModel> GetListAsync()
    {
        var model = new VoucherListViewModel();
        model.CanSave = User.Identity?.IsAuthenticated == true;

        if (model.CanSave)
        {
            model.Vouchers = (await _promotions.GetForOrderAsync(User.UserId(), 0, false)).ToList();
            return model;
        }

        var now = DateTime.UtcNow;
        model.Vouchers = await (from p in _context.Promotions
                                where p.IsActive && p.StartsAtUtc <= now && p.EndsAtUtc >= now
                                orderby p.EndsAtUtc
                                select new VoucherDto(p.Id, p.Code, p.Name, p.DiscountType.ToString(),
                                    p.Value, p.MaxDiscount, p.MinOrderAmount, p.EndsAtUtc,
                                    false, true, null, 0m))
                               .ToListAsync();
        return model;
    }
}
