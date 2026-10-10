using CineBook.Application.Services;
using CineBook.Web.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Web.Controllers;

[Authorize(Roles = "Staff,CinemaManager,Admin")]
public class CheckInController : Controller
{
    private readonly CheckInService _checkIn;

    public CheckInController(CheckInService checkIn)
    {
        _checkIn = checkIn;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan(string qrPayload)
    {
        var result = await _checkIn.ScanAsync(qrPayload ?? "", User.UserId(), User.CinemaId());
        return PartialView("_CheckInResult", result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ByCode(string code)
    {
        var result = await _checkIn.ScanByCodeAsync(code ?? "", User.UserId(), User.CinemaId());
        return PartialView("_CheckInResult", result);
    }
}
