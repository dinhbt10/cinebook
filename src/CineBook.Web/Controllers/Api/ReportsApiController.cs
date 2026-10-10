using CineBook.Web.Common;
using CineBook.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Web.Controllers.Api;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = Roles.AdminOrManager)]
public class ReportsApiController(ReportService reports) : ControllerBase
{
    private Guid? Scope() => User.ManagedCinemaId();

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct) =>
        Ok(await reports.GetDashboardAsync(Scope(), ct));

    [HttpGet("revenue-by-day")]
    public async Task<IActionResult> RevenueByDay(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        Ok(await reports.GetRevenueByDayAsync(from, to, Scope(), ct));

    [HttpGet("revenue-by-movie")]
    public async Task<IActionResult> RevenueByMovie(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        Ok(await reports.GetRevenueByMovieAsync(from, to, Scope(), ct));
}
