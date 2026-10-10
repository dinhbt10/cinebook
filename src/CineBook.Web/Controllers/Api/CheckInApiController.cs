using CineBook.Web.Common;
using CineBook.Application.Dtos;
using CineBook.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Web.Controllers.Api;

[ApiController]
[Route("api/checkin")]
[Authorize(Roles = Roles.StaffOrManager)]
public class CheckInApiController(CheckInService checkIn) : ControllerBase
{
    [HttpPost("scan")]
    public async Task<IActionResult> Scan(CheckInRequest req, CancellationToken ct) =>
        Ok(await checkIn.ScanAsync(req.QrPayload, User.UserId(), User.CinemaId(), ct));

    [HttpPost("by-code/{code}")]
    public async Task<IActionResult> ByCode(string code, CancellationToken ct) =>
        Ok(await checkIn.ScanByCodeAsync(code, User.UserId(), User.CinemaId(), ct));
}
