using System.Security.Claims;
using CineBook.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Api.Common;

public static class ApiExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Token không chứa mã người dùng.");

    /// <summary>BR-19: nhân viên và quản lý rạp bị giới hạn trong rạp mình phụ trách.</summary>
    public static Guid? CinemaId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("cinema_id"), out var id) ? id : null;

    public static IActionResult ToActionResult(this Result result) =>
        result.Succeeded
            ? new OkResult()
            : Problem(result);

    public static IActionResult ToActionResult<T>(this Result<T> result) =>
        result.Succeeded
            ? new OkObjectResult(result.Value)
            : Problem(result);

    private static IActionResult Problem(Result result)
    {
        var status = result.ErrorCode switch
        {
            "NOT_FOUND" or "MOVIE_NOT_FOUND" or "ROOM_NOT_FOUND" or "SHOWTIME_NOT_FOUND"
                or "CINEMA_NOT_FOUND" or "TXN_NOT_FOUND" => StatusCodes.Status404NotFound,
            "SEATS_TAKEN" or "SHOWTIME_OVERLAP" or "PROMO_CODE_TAKEN" => StatusCodes.Status409Conflict,
            "INVALID_CREDENTIALS" or "BAD_SIGNATURE" => StatusCodes.Status401Unauthorized,
            "ACCOUNT_LOCKED" => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };

        return new ObjectResult(new { code = result.ErrorCode, message = result.Error })
        {
            StatusCode = status
        };
    }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "CinemaManager";
    public const string Staff = "Staff";
    public const string AdminOrManager = "Admin,CinemaManager";
    public const string StaffOrManager = "Staff,CinemaManager,Admin";
}
