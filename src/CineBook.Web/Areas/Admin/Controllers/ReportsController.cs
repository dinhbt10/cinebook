using CineBook.Application.Services;
using CineBook.Web.Areas.Admin.Models;
using CineBook.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin,CinemaManager")]
public class ReportsController : Controller
{
    private readonly ReportService _reports;

    public ReportsController(ReportService reports)
    {
        _reports = reports;
    }

    public async Task<IActionResult> Index()
    {
        var to = Fmt.VnToday;
        var from = to.AddDays(-29);
        var model = await BuildAsync(from, to);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Data(DateOnly? from, DateOnly? to)
    {
        var model = await BuildAsync(from, to);
        return PartialView("_ReportData", model);
    }

    private async Task<ReportsViewModel> BuildAsync(DateOnly? from, DateOnly? to)
    {
        var model = new ReportsViewModel();
        model.From = from;
        model.To = to;

        if (from != null && to != null)
        {
            var scope = User.ManagedCinemaId();
            model.ByDay = (await _reports.GetRevenueByDayAsync(from.Value, to.Value, scope)).ToList();
            model.ByMovie = (await _reports.GetRevenueByMovieAsync(from.Value, to.Value, scope)).ToList();
        }

        model.TotalTicket = model.ByDay.Sum(r => r.TicketRevenue);
        model.TotalCombo = model.ByDay.Sum(r => r.ConcessionRevenue);
        return model;
    }
}
