using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CineBook.Infrastructure.BackgroundJobs;

/// <summary>
/// Tự gọi chính mình định kỳ để nền tảng không cho tiến trình ngủ.
///
/// Gói miễn phí của Render tắt máy sau 15 phút không có yêu cầu nào đi vào, đánh thức
/// lại mất khoảng 50 giây — quá lâu cho một buổi bảo vệ. Lời gọi ở đây đi ra Internet
/// rồi vòng lại qua bộ cân bằng tải của Render nên được tính là lưu lượng đi vào,
/// đủ để giữ máy thức.
///
/// Giới hạn cần biết: cách này chỉ NGĂN máy ngủ chứ không ĐÁNH THỨC được máy đã ngủ,
/// vì lúc đó chính tiến trình này cũng đã dừng. Lịch gọi từ bên ngoài
/// (.github/workflows/keep-api-awake.yml) mới lo được việc đánh thức.
///
/// Ở máy cá nhân dịch vụ này tự tắt: không có địa chỉ công khai thì không chạy.
/// </summary>
public class KeepAwakeService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<KeepAwakeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Render tự tiêm RENDER_EXTERNAL_URL nên không phải cấu hình tay.
        var url = config["KeepAwake:Url"]
            ?? Environment.GetEnvironmentVariable("RENDER_EXTERNAL_URL");

        if (string.IsNullOrWhiteSpace(url))
        {
            logger.LogInformation(
                "Không có địa chỉ công khai, bỏ qua việc tự gọi giữ máy thức.");
            return;
        }

        if (!config.GetValue("KeepAwake:Enabled", true))
        {
            logger.LogInformation("Tự gọi giữ máy thức đang bị tắt bằng cấu hình.");
            return;
        }

        var minutes = Math.Clamp(config.GetValue("KeepAwake:IntervalMinutes", 10), 1, 14);
        var target = $"{url.TrimEnd('/')}/api/health";

        logger.LogInformation("Giữ máy thức: gọi {Target} mỗi {Minutes} phút.", target, minutes);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var http = httpClientFactory.CreateClient(nameof(KeepAwakeService));
                http.Timeout = TimeSpan.FromSeconds(30);

                using var res = await http.GetAsync(target, stoppingToken);
                logger.LogDebug("Giữ máy thức: {Status}", (int)res.StatusCode);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Hỏng một nhịp không phải chuyện lớn, tuyệt đối không được để
                // ngoại lệ ở đây làm sập cả tiến trình.
                logger.LogWarning(ex, "Giữ máy thức: gọi không thành công, sẽ thử lại.");
            }
        }
    }
}
