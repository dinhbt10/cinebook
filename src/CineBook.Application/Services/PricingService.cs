using CineBook.Application.Abstractions;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Application.Services;

public class PricingService(IAppDbContext db)
{
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

    private static bool InRange(TimeOnly value, TimeOnly from, TimeOnly to) =>
        from <= to ? value >= from && value < to
                   : value >= from || value < to;

    public static DateTime LocalDateStartUtc(DateOnly localDate) =>
        DateTime.SpecifyKind(
            localDate.ToDateTime(TimeOnly.MinValue).AddHours(-VietnamUtcOffsetHours),
            DateTimeKind.Utc);

    public static decimal RoundToThousand(decimal amount) =>
        Math.Round(amount / 1000m, MidpointRounding.AwayFromZero) * 1000m;
}
