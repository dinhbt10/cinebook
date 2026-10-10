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
public class ShowtimesController : Controller
{
    private const int VnOffsetHours = 7;

    private readonly CineBookDbContext _context;
    private readonly ShowtimeService _showtimes;
    private readonly RoomService _rooms;

    public ShowtimesController(CineBookDbContext context, ShowtimeService showtimes, RoomService rooms)
    {
        _context = context;
        _showtimes = showtimes;
        _rooms = rooms;
    }

    public async Task<IActionResult> Index(DateOnly? date, Guid? cinemaId)
    {
        var model = await BuildPageAsync(date ?? Fmt.VnToday, cinemaId, new ShowtimeFormViewModel());
        ViewBag.Notice = TempData["Notice"];
        ViewBag.Error = TempData["Error"];
        return View(model);
    }

    public async Task<IActionResult> List(DateOnly date, Guid? cinemaId)
    {
        var list = await _showtimes.SearchForAdminAsync(cinemaId, date, User.ManagedCinemaId());
        return PartialView("_ShowtimeList", list.ToList());
    }

    public async Task<IActionResult> Rooms(Guid cinemaId)
    {
        var scope = User.ManagedCinemaId();
        if (scope != null && scope != cinemaId)
        {
            return Json(new List<RoomDto>());
        }

        var rooms = await _rooms.GetByCinemaAsync(cinemaId);
        return Json(rooms.Select(r => new
        {
            r.Id,
            Label = r.Name + " · " + Fmt.Label(Fmt.RoomTypeLabel, r.RoomType) + " · " + r.SeatCount + " ghế"
        }));
    }

    public async Task<IActionResult> PricePreview(Guid roomId, DateTime startsAtUtc, decimal basePrice)
    {
        var result = await _showtimes.PreviewPricesAsync(roomId, startsAtUtc, basePrice);
        if (!result.Succeeded)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { message = result.Error });
        }
        return Json(result.Value);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ShowtimeFormViewModel model)
    {
        var filterDate = model.FilterDate ?? Fmt.VnToday;

        if (ModelState.IsValid)
        {
            var request = new CreateShowtimeRequest(
                model.MovieId!.Value,
                model.RoomId!.Value,
                ToUtc(model.StartsLocal!.Value),
                model.BasePrice!.Value,
                model.SubtitleMode,
                model.OpenForSale,
                new SeatTypePrices(model.PriceStandard!.Value, model.PriceVip!.Value, model.PriceCouple!.Value));

            var result = await _showtimes.CreateAsync(request, User.ManagedCinemaId());
            if (result.Succeeded)
            {
                return RedirectToAction("Index", new { date = Fmt.Iso(filterDate), cinemaId = model.FilterCinemaId });
            }
            ModelState.AddModelError("", result.Error!);
        }

        var page = await BuildPageAsync(filterDate, model.FilterCinemaId, model);
        page.OpenCreate = true;
        return View("Index", page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetOnSale(Guid id, bool value)
    {
        var result = await _showtimes.SetOnSaleAsync(id, value, User.ManagedCinemaId());
        if (!result.Succeeded)
        {
            return Json(new { ok = false, message = result.Error });
        }
        return Json(new { ok = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(CancelShowtimeViewModel model)
    {
        var back = new { date = Fmt.Iso(model.FilterDate ?? Fmt.VnToday), cinemaId = model.FilterCinemaId };

        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction("Index", back);
        }

        var result = await _showtimes.CancelAsync(model.Id, model.Reason.Trim(), User.ManagedCinemaId(), User.UserId());
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction("Index", back);
        }

        TempData["Notice"] = result.Value > 0
            ? "Đã hủy suất chiếu và hoàn 100% cho " + result.Value + " đơn đã thanh toán."
            : "Đã hủy suất chiếu.";
        return RedirectToAction("Index", back);
    }

    private async Task<ShowtimesPageViewModel> BuildPageAsync(DateOnly date, Guid? cinemaId, ShowtimeFormViewModel form)
    {
        var scope = User.ManagedCinemaId();

        var cinemaQuery = _context.Cinemas.Where(c => c.IsActive);
        if (scope != null)
        {
            cinemaQuery = cinemaQuery.Where(c => c.Id == scope);
        }
        var cinemas = await cinemaQuery
            .OrderBy(c => c.City).ThenBy(c => c.Name)
            .Select(c => new CinemaDto(c.Id, c.Name, c.City, c.District, c.Address, c.Hotline))
            .ToListAsync();

        var movies = await (from m in _context.Movies
                            where m.Status == MovieStatus.NowShowing || m.Status == MovieStatus.ComingSoon
                            orderby m.ReleaseDate descending
                            select new { m.Id, m.Title, m.DurationMinutes })
                           .ToListAsync();
        ViewBag.Movies = movies.Select(m => new KeyValuePair<Guid, string>(m.Id, m.Title + " (" + m.DurationMinutes + "′)")).ToList();

        var rooms = new List<RoomDto>();
        if (form.CinemaId != null && (scope == null || scope == form.CinemaId))
        {
            rooms = (await _rooms.GetByCinemaAsync(form.CinemaId.Value)).ToList();
        }
        ViewBag.Rooms = rooms;
        ViewBag.Cinemas = cinemas;

        form.FilterDate = date;
        form.FilterCinemaId = cinemaId;

        var model = new ShowtimesPageViewModel();
        model.Date = date;
        model.CinemaId = cinemaId;
        model.Cinemas = cinemas;
        model.Showtimes = (await _showtimes.SearchForAdminAsync(cinemaId, date, scope)).ToList();
        model.Form = form;
        return model;
    }

    private static DateTime ToUtc(DateTime vnLocal)
    {
        return DateTime.SpecifyKind(vnLocal.AddHours(-VnOffsetHours), DateTimeKind.Utc);
    }
}
