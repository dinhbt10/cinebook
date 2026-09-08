using CineBook.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CineBook.Infrastructure.BackgroundJobs;

/// <summary>
/// Chạy mỗi phút để dọn đơn hàng quá hạn giữ ghế.
///
/// Lưu ý quan trọng: ghế đã tự động được coi là trống ngay khi HoldExpiresAtUtc
/// trôi qua, vì câu UPDATE giành ghế có điều kiện kiểm tra mốc này. Job này chỉ
/// để trạng thái đơn hàng trong CSDL phản ánh đúng thực tế — thiếu nó thì đơn
/// treo mãi ở PENDING và báo cáo doanh thu sẽ sai.
/// </summary>
public class ExpireBookingsService(
    IServiceScopeFactory scopeFactory,
    ILogger<ExpireBookingsService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
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
    }
}
