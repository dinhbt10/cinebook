using CineBook.Application.Common;
using CineBook.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CineBook.Infrastructure.BackgroundJobs;

public class ExpireBookingsService(
    IServiceScopeFactory scopeFactory,
    IOptions<BookingOptions> options,
    ILogger<ExpireBookingsService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = Math.Max(1, options.Value.ExpireJobIntervalMinutes);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var bookings = scope.ServiceProvider.GetRequiredService<BookingService>();
                await bookings.ExpireOverdueBookingsAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Lỗi khi dọn đơn hàng quá hạn");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
