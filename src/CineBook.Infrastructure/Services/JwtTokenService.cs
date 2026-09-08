using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CineBook.Application.Abstractions;
using CineBook.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CineBook.Infrastructure.Services;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Key { get; set; } = "doi-khoa-nay-trong-cau-hinh-that-it-nhat-32-ky-tu";
    public string Issuer { get; set; } = "CineBook";
    public string Audience { get; set; } = "CineBookClient";
    public int ExpiresHours { get; set; } = 8;
}

public class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _opt = options.Value;

    public string CreateAccessToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        // BR-19: nhân viên và quản lý rạp mang theo mã rạp mình phụ trách.
        if (user.CinemaId is { } cinemaId)
            claims.Add(new Claim("cinema_id", cinemaId.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.Key));
        var token = new JwtSecurityToken(
            issuer: _opt.Issuer,
            audience: _opt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_opt.ExpiresHours),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
