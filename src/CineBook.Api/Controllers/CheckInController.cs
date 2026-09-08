using CineBook.Api.Common;
using CineBook.Application.Dtos;
using CineBook.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Api.Controllers;

/// <summary>Trang soát vé của nhân viên rạp — §07 tài liệu flow.</summary>
[ApiController]
[Route("api/checkin")]
[Authorize(Roles = Roles.StaffOrManager)]
public class CheckInController(CheckInService checkIn) : ControllerBase
{
    [HttpPost("scan")]
    public async Task<IActionResult> Scan(CheckInRequest req, CancellationToken ct) =>
        Ok(await checkIn.ScanAsync(req.QrPayload, User.UserId(), User.CinemaId(), ct));

    /// <summary>Tra cứu bằng mã vé khi mã QR mờ không quét được.</summary>
    [HttpPost("by-code/{code}")]
    public async Task<IActionResult> ByCode(string code, CancellationToken ct) =>
        Ok(await checkIn.ScanByCodeAsync(code, User.UserId(), User.CinemaId(), ct));
}
