using CineBook.Web.Common;
using CineBook.Application.Dtos;
using CineBook.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Web.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthApiController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest req, CancellationToken ct) =>
        (await auth.RegisterAsync(req, ct)).ToActionResult();

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req, CancellationToken ct) =>
        (await auth.LoginAsync(req, ct)).ToActionResult();

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var me = await auth.GetMeAsync(User.UserId(), ct);
        return me is null ? NotFound() : Ok(me);
    }
}
