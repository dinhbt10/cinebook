using CineBook.Application.Common;
using CineBook.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CineBook.Infrastructure.BackgroundJobs;

/// <summary>
/// Dọn đơn hàng quá hạn giữ ghế: chạy một lần lúc khởi động, sau đó theo chu kỳ
/// Booking:ExpireJobIntervalMinutes.
///
/// Lưu ý quan trọng: ghế đã tự động được coi là trống ngay khi HoldExpiresAtUtc
/// trôi qua, vì câu UPDATE giành ghế có điều kiện kiểm tra mốc này. Job này chỉ
/// để trạng thái đơn hàng trong CSDL phản ánh đúng thực tế — thiếu nó thì đơn
/// treo mãi ở PENDING và báo cáo doanh thu sẽ sai.
///
/// Vì sao không chạy mỗi phút: Neon chỉ ngủ khi 5 phút liền không có query nào.
/// Chạy mỗi phút cộng với việc giữ máy Render thức thì Neon thức 24/7, tốn khoảng
/// 6 CU-giờ mỗi ngày và hết quota gói miễn phí sau chừng hai tuần rưỡi.
/// Chạy lúc khởi động là để máy Render ngủ/thức liên tục (mỗi phiên ngắn hơn một
/// chu kỳ) vẫn dọn được đơn.
/// </summary>
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
