using System.Security.Claims;
using CineBook.Application.Dtos;
using CineBook.Application.Services;
using CineBook.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Web.Controllers;

public class AccountController : Controller
{
    private readonly AuthService _auth;

    public AccountController(AuthService auth)
    {
        _auth = auth;
    }

    [HttpGet]
    public IActionResult Login(string? next)
    {
        ViewBag.Next = next;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? next)
    {
        ViewBag.Next = next;
        if (!ModelState.IsValid)
            return View(model);

        var result = await _auth.LoginAsync(new LoginRequest(model.Email, model.Password));
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        var user = result.Value!.User;
        await SignInAsync(user);

        if (!string.IsNullOrEmpty(next) && Url.IsLocalUrl(next))
            return Redirect(next);

        if (user.Role == "Staff")
            return RedirectToAction("Index", "CheckIn");
        if (user.Role == "Admin" || user.Role == "CinemaManager")
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var phone = string.IsNullOrWhiteSpace(model.Phone) ? null : model.Phone;
        var result = await _auth.RegisterAsync(
            new RegisterRequest(model.Email, model.Password, model.FullName, phone));
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        await SignInAsync(result.Value!.User);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task SignInAsync(UserDto user)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        };
        if (user.CinemaId != null)
            claims.Add(new Claim("cinema_id", user.CinemaId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}
