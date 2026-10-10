using CineBook.Application.Dtos;
using CineBook.Application.Services;
using CineBook.Domain.Enums;
using CineBook.Infrastructure.Persistence;
using CineBook.Web.Areas.Admin.Models;
using CineBook.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,CinemaManager")]
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
        if (!User.IsInRole("Admin"))
            return View("AdminOnly");

        ViewBag.Notice = TempData["Notice"];
        ViewBag.Error = TempData["Error"];
        return View(await BuildAsync(null));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Create()
    {
        var now = DateTime.UtcNow;
        var form = new PromotionFormViewModel();
        form.StartsLocal = ToVnMinute(now);
        form.EndsLocal = ToVnMinute(now.AddDays(30));
        return PartialView("_PromotionForm", form);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PromotionFormViewModel form)
    {
        IgnoreMaxDiscountWhenFixed(form);
        if (ModelState.IsValid)
        {
            var result = await _promotions.CreateAsync(ToRequest(form));
            if (result.Succeeded)
                return RedirectToAction("Index");

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        return View("Index", await BuildAsync(form));
    }

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Edit(Guid id)
    {
        var promo = await _context.Promotions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (promo == null)
            return NotFound();

        var form = new PromotionFormViewModel();
        form.Code = promo.Code;
        form.Name = promo.Name;
        form.DiscountType = promo.DiscountType;
        form.Value = promo.Value;
        form.MaxDiscount = promo.MaxDiscount;
        form.MinOrderAmount = promo.MinOrderAmount;
        form.StartsLocal = ToVnMinute(promo.StartsAtUtc);
        form.EndsLocal = ToVnMinute(promo.EndsAtUtc);
        form.UsageLimit = promo.UsageLimit;
        form.PerUserLimit = promo.PerUserLimit;
        form.IsActive = promo.IsActive;
        await FillEditInfoAsync(form, id);
        return PartialView("_PromotionForm", form);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, PromotionFormViewModel form)
    {
        if (!await FillEditInfoAsync(form, id))
            return NotFound();

        IgnoreMaxDiscountWhenFixed(form);
        if (ModelState.IsValid)
        {
            var result = await _promotions.UpdateAsync(id, ToRequest(form));
            if (result.Succeeded)
                return RedirectToAction("Index");

            ModelState.AddModelError(string.Empty, result.Error!);
        }

        return View("Index", await BuildAsync(form));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(Guid id, bool value)
    {
        var result = await _promotions.SetActiveAsync(id, value);
        if (!result.Succeeded)
            TempData["Error"] = result.Error;
        return RedirectToAction("Index");
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await _promotions.DeleteAsync(id);
        if (!result.Succeeded)
            TempData["Error"] = result.Error;
        else if (!result.Value)
            TempData["Notice"] = "Voucher đã có đơn sử dụng nên được chuyển sang ngừng áp dụng thay vì xoá.";
        return RedirectToAction("Index");
    }

    private async Task<PromotionsIndexViewModel> BuildAsync(PromotionFormViewModel? form)
    {
        var model = new PromotionsIndexViewModel();
        model.Items = (await _promotions.GetAllForAdminAsync()).ToList();
        model.Form = form;
        return model;
    }

    private async Task<bool> FillEditInfoAsync(PromotionFormViewModel form, Guid id)
    {
        var info = await (from p in _context.Promotions
                          where p.Id == id
                          select new
                          {
                              p.Code,
                              p.UsedCount,
                              Locked = _context.Bookings.Any(b => b.PromotionId == p.Id)
                          }).FirstOrDefaultAsync();
        if (info == null)
            return false;

        form.Id = id;
        form.OriginalCode = info.Code;
        form.UsedCount = info.UsedCount;
        form.Locked = info.Locked;
        return true;
    }

    private void IgnoreMaxDiscountWhenFixed(PromotionFormViewModel form)
    {
        if (form.DiscountType == DiscountType.FixedAmount)
        {
            form.MaxDiscount = null;
            ModelState.Remove("MaxDiscount");
        }
    }

    private static SavePromotionRequest ToRequest(PromotionFormViewModel form)
    {
        var isPercent = form.DiscountType == DiscountType.Percentage;
        return new SavePromotionRequest(
            form.Code.Trim().ToUpperInvariant(),
            form.Name.Trim(),
            form.DiscountType,
            form.Value!.Value,
            isPercent ? form.MaxDiscount : null,
            form.MinOrderAmount ?? 0,
            FromVnLocal(form.StartsLocal!.Value),
            FromVnLocal(form.EndsLocal!.Value),
            form.UsageLimit,
            form.PerUserLimit!.Value,
            form.IsActive);
    }

    private static DateTime ToVnMinute(DateTime utc)
    {
        var vn = Fmt.ToVn(utc);
        return new DateTime(vn.Year, vn.Month, vn.Day, vn.Hour, vn.Minute, 0);
    }

    private static DateTime FromVnLocal(DateTime local)
    {
        return DateTime.SpecifyKind(local.AddHours(-7), DateTimeKind.Utc);
    }
}
