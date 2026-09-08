using CineBook.Application.Abstractions;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Application.Services;

/// <summary>
/// BR-18: giá vé = giá cơ sở của suất chiếu × hệ số của các quy tắc khớp + phụ thu.
/// Giá được chốt ngay khi tạo suất chiếu và lưu vào từng dòng ShowtimeSeat,
/// nên sơ đồ ghế hiển thị giá thật và giá không đổi giữa chừng khi khách đang đặt.
/// </summary>
public class PricingService(IAppDbContext db)
{
    /// <summary>Giờ Việt Nam so với UTC. Không có quy ước giờ mùa hè nên dùng hằng số.</summary>
    public const int VietnamUtcOffsetHours = 7;

    public async Task<IReadOnlyList<PriceRule>> LoadActiveRulesAsync(CancellationToken ct = default) =>
        await db.PriceRules.AsNoTracking()
            .Where(r => r.IsActive)
            .OrderBy(r => r.Priority)
            .ToListAsync(ct);

    public decimal CalculateSeatPrice(
        decimal basePrice, DateTime startsAtUtc, Guid cinemaId,
        RoomType roomType, SeatType seatType, IReadOnlyList<PriceRule> rules)
    {
        var local = startsAtUtc.AddHours(VietnamUtcOffsetHours);
        var localTime = TimeOnly.FromDateTime(local);
        var localDow = (int)local.DayOfWeek;

        decimal multiplier = 1m;
        decimal surcharge = 0m;

        foreach (var r in rules)
        {
            if (r.CinemaId is { } cid && cid != cinemaId) continue;
            if (r.RoomType is { } rt && rt != roomType) continue;
            if (r.SeatType is { } st && st != seatType) continue;
            if (r.DayOfWeek is { } dow && dow != localDow) continue;
            if (r.TimeFrom is { } from && r.TimeTo is { } to && !InRange(localTime, from, to)) continue;

            multiplier *= r.Multiplier;
            surcharge += r.Surcharge;
        }

        return RoundToThousand(basePrice * multiplier + surcharge);
    }

    /// <summary>Khoảng giờ có thể vắt qua nửa đêm, ví dụ 22:00 → 02:00.</summary>
    private static bool InRange(TimeOnly value, TimeOnly from, TimeOnly to) =>
        from <= to ? value >= from && value < to
                   : value >= from || value < to;

    /// <summary>
    /// Đổi một ngày theo giờ Việt Nam thành mốc UTC đầu ngày.
    /// Bắt buộc gắn Kind = Utc: PostgreSQL kiểu timestamptz từ chối DateTime
    /// không xác định múi giờ (rủi ro #5 trong tài liệu đánh giá công nghệ).
    /// </summary>
    public static DateTime LocalDateStartUtc(DateOnly localDate) =>
        DateTime.SpecifyKind(
            localDate.ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);

    /// <summary>Rạp Việt Nam niêm yết giá theo bội số nghìn đồng.</summary>
    public static decimal RoundToThousand(decimal amount) =>
        Math.Round(amount / 1000m, MidpointRounding.AwayFromZero) * 1000m;
}
