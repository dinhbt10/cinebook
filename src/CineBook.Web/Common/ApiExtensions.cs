using System.Security.Claims;
using CineBook.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Web.Common;

public static class ApiExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw new UnauthorizedAccessException("Token không chứa mã người dùng.");

    public static Guid? CinemaId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue("cinema_id"), out var id) ? id : null;

    public static Guid? ManagedCinemaId(this ClaimsPrincipal user) =>
        user.IsInRole(Roles.Admin) ? null : user.CinemaId() ?? Guid.Empty;

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
            "ACCOUNT_LOCKED" or "FORBIDDEN_CINEMA" => StatusCodes.Status403Forbidden,
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
