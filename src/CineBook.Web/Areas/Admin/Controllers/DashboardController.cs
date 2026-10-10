using System.Globalization;
using CineBook.Application.Services;
using CineBook.Web.Areas.Admin.Models;
using CineBook.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,CinemaManager")]
public class DashboardController : Controller
{
    private readonly ReportService _reports;

    public DashboardController(ReportService reports)
    {
        _reports = reports;
    }

    public async Task<IActionResult> Index()
    {
        var to = Fmt.VnToday;
        var from = to.AddDays(-13);

        var model = new DashboardViewModel();
        model.Stats = await _reports.GetDashboardAsync(User.ManagedCinemaId());
        model.TopMovies = (await _reports.GetRevenueByMovieAsync(from, to, User.ManagedCinemaId()))
            .Take(6)
            .ToList();
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Revenue()
    {
        var to = Fmt.VnToday;
        var from = to.AddDays(-13);

        var points = await _reports.GetRevenueByDayAsync(from, to, User.ManagedCinemaId());
        var chart = points.Select(p => new
        {
            date = p.Date.ToString("dd/MM", CultureInfo.InvariantCulture),
            total = p.TicketRevenue + p.ConcessionRevenue
        });
        return Json(chart);
    }
}
