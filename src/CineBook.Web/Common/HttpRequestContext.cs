using CineBook.Application.Abstractions;

namespace CineBook.Web.Common;

public class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    public string? ClientIpAddress
    {
        get
        {
            var ctx = accessor.HttpContext;
            if (ctx is null) return null;

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
