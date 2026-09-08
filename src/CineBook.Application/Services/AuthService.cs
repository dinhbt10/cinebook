using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Application.Dtos;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Application.Services;

public class AuthService(
    IAppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    IDateTimeProvider clock)
{
    public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        var email = req.Email.Trim().ToLowerInvariant();

        if (req.Password.Length < 6)
            return Result<AuthResponse>.Fail("WEAK_PASSWORD", "Mật khẩu phải có ít nhất 6 ký tự.");

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Result<AuthResponse>.Fail("EMAIL_TAKEN", "Email này đã được đăng ký.");

        var user = new User
        {
            Email = email,
            PasswordHash = hasher.Hash(req.Password),
            FullName = req.FullName.Trim(),
            Phone = req.Phone?.Trim(),
            Role = UserRole.Customer,
            EmailVerified = true // Bản BTL bỏ qua bước xác thực email.
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return Result<AuthResponse>.Ok(BuildResponse(user));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null || !hasher.Verify(req.Password, user.PasswordHash))
            return Result<AuthResponse>.Fail("INVALID_CREDENTIALS", "Email hoặc mật khẩu không đúng.");

        if (!user.IsActive)
            return Result<AuthResponse>.Fail("ACCOUNT_LOCKED", "Tài khoản đã bị khóa.");

        return Result<AuthResponse>.Ok(BuildResponse(user));
    }

    public async Task<UserDto?> GetMeAsync(Guid userId, CancellationToken ct = default)
    {
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, ct);
        return u is null ? null : ToDto(u);
    }

    private AuthResponse BuildResponse(User user) =>
        new(tokens.CreateAccessToken(user), clock.UtcNow.AddHours(8), ToDto(user));

    private static UserDto ToDto(User u) =>
        new(u.Id, u.Email, u.FullName, u.Phone, u.Role.ToString(), u.CinemaId);
}
