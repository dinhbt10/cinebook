using CineBook.Application.Abstractions;

namespace CineBook.Api.Common;

/// <summary>
/// Cài đặt <see cref="IRequestContext"/> bằng ASP.NET Core.
/// Đăng ký ở tầng Api để Application và Infrastructure vẫn không biết gì về HTTP.
/// </summary>
public class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public string? ClientIpAddress
    {
        get
        {
            var ctx = accessor.HttpContext;
            if (ctx is null) return null;

            // Khi chạy sau reverse proxy (Nginx, Cloudflare) thì IP thật nằm ở header này.
            var forwarded = ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(forwarded))
                return forwarded.Split(',')[0].Trim();

            return ctx.Connection.RemoteIpAddress?.MapToIPv4().ToString();
        }
    }

    public string? BaseUrl
    {
        get
        {
            var r = accessor.HttpContext?.Request;
            return r is null ? null : $"{r.Scheme}://{r.Host}{r.PathBase}";
        }
    }
}
