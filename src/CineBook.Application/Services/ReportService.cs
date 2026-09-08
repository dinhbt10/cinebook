using CineBook.Application.Abstractions;
using CineBook.Application.Dtos;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Application.Services;

/// <summary>
/// Báo cáo cho trang quản trị. Tỷ lệ lấp đầy (occupancy) là chỉ số quan trọng
/// nhất của một rạp mà bản mô tả nghiệp vụ ban đầu chưa có.
/// </summary>
public class ReportService(IAppDbContext db, IDateTimeProvider clock)
{
    public async Task<DashboardDto> GetDashboardAsync(Guid? cinemaId, CancellationToken ct = default)
    {
        var (todayFrom, todayTo) = LocalDayRangeUtc(DateOnly.FromDateTime(LocalNow()));
        var monthFrom = LocalDayRangeUtc(new DateOnly(LocalNow().Year, LocalNow().Month, 1)).from;

        var paid = db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Paid || b.Status == BookingStatus.Refunded)
            .Where(b => b.PaidAtUtc != null);

        if (cinemaId is { } cid)
            paid = paid.Where(b => b.Showtime.Room.CinemaId == cid);

        var today = paid.Where(b => b.PaidAtUtc >= todayFrom && b.PaidAtUtc < todayTo);

        var revenueToday = await today.SumAsync(b => (decimal?)b.Total, ct) ?? 0m;
        var revenueMonth = await paid.Where(b => b.PaidAtUtc >= monthFrom)
            .SumAsync(b => (decimal?)b.Total, ct) ?? 0m;
        var bookingsToday = await today.CountAsync(ct);
        var ticketsToday = await db.Tickets.AsNoTracking()
            .CountAsync(t => t.Booking.PaidAtUtc >= todayFrom && t.Booking.PaidAtUtc < todayTo, ct);

        var showtimesQuery = db.Showtimes.AsNoTracking()
            .Where(s => !s.IsCancelled && s.StartsAtUtc >= todayFrom && s.StartsAtUtc < todayTo);
        if (cinemaId is { } cid2) showtimesQuery = showtimesQuery.Where(s => s.Room.CinemaId == cid2);

        var showtimesToday = await showtimesQuery.CountAsync(ct);
        var seatsToday = await showtimesQuery.SelectMany(s => s.Seats).CountAsync(ct);
        var soldToday = await showtimesQuery.SelectMany(s => s.Seats)
            .CountAsync(x => x.Status == SeatStatus.Sold, ct);

        var occupancy = seatsToday == 0 ? 0 : (double)soldToday / seatsToday;

        return new DashboardDto(revenueToday, revenueMonth, ticketsToday,
            bookingsToday, Math.Round(occupancy, 4), showtimesToday);
    }

    public async Task<IReadOnlyList<RevenuePointDto>> GetRevenueByDayAsync(
        DateOnly from, DateOnly to, Guid? cinemaId, CancellationToken ct = default)
    {
        var fromUtc = LocalDayRangeUtc(from).from;
        var toUtc = LocalDayRangeUtc(to).to;

        var q = db.Bookings.AsNoTracking()
            .Where(b => b.Status == BookingStatus.Paid && b.PaidAtUtc >= fromUtc && b.PaidAtUtc < toUtc);
        if (cinemaId is { } cid) q = q.Where(b => b.Showtime.Room.CinemaId == cid);

        var rows = await q.Select(b => new
        {
            b.PaidAtUtc,
            Ticket = b.SeatsSubtotal - b.DiscountAmount,
            Concession = b.ConcessionSubtotal
        }).ToListAsync(ct);

        return rows
            .GroupBy(r => DateOnly.FromDateTime(
                r.PaidAtUtc!.Value.AddHours(PricingService.VietnamUtcOffsetHours)))
            .OrderBy(g => g.Key)
            .Select(g => new RevenuePointDto(g.Key,
                g.Sum(x => x.Ticket), g.Sum(x => x.Concession), g.Count()))
            .ToList();
    }

    public async Task<IReadOnlyList<MovieRevenueDto>> GetRevenueByMovieAsync(
        DateOnly from, DateOnly to, Guid? cinemaId, CancellationToken ct = default)
    {
        var fromUtc = LocalDayRangeUtc(from).from;
        var toUtc = LocalDayRangeUtc(to).to;

        var q = db.Showtimes.AsNoTracking()
            .Where(s => !s.IsCancelled && s.StartsAtUtc >= fromUtc && s.StartsAtUtc < toUtc);
        if (cinemaId is { } cid) q = q.Where(s => s.Room.CinemaId == cid);

        var rows = await q.Select(s => new
        {
            s.MovieId,
            s.Movie.Title,
            Total = s.Seats.Count,
            Sold = s.Seats.Count(x => x.Status == SeatStatus.Sold),
            Revenue = s.Seats.Where(x => x.Status == SeatStatus.Sold).Sum(x => (decimal?)x.Price) ?? 0m
        }).ToListAsync(ct);

        return rows.GroupBy(r => new { r.MovieId, r.Title })
            .Select(g => new MovieRevenueDto(
                g.Key.MovieId, g.Key.Title,
                g.Sum(x => x.Revenue),
                g.Sum(x => x.Sold),
                g.Sum(x => x.Total) == 0 ? 0 : Math.Round((double)g.Sum(x => x.Sold) / g.Sum(x => x.Total), 4)))
            .OrderByDescending(x => x.Revenue)
            .ToList();
    }

    private DateTime LocalNow() => clock.UtcNow.AddHours(PricingService.VietnamUtcOffsetHours);

    private static (DateTime from, DateTime to) LocalDayRangeUtc(DateOnly localDate)
    {
        var from = PricingService.LocalDateStartUtc(localDate);
        return (from, from.AddDays(1));
    }
}
