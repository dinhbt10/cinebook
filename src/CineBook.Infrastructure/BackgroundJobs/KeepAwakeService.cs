using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CineBook.Infrastructure.BackgroundJobs;

public class KeepAwakeService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<KeepAwakeService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                logger.LogWarning(ex, "Giữ máy thức: gọi không thành công, sẽ thử lại.");
            }
        }
    }
}
