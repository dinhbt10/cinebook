using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Application.Services;
using CineBook.Infrastructure.BackgroundJobs;
using CineBook.Infrastructure.Payments;
using CineBook.Infrastructure.Persistence;
using CineBook.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CineBook.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCineBook(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("Default")
            ?? Environment.GetEnvironmentVariable("CINEBOOK_DB")
            ?? throw new InvalidOperationException(
                "Chưa cấu hình chuỗi kết nối. Đặt ConnectionStrings:Default trong user-secrets " +
                "hoặc biến môi trường CINEBOOK_DB — xem README.");

        services.AddDbContext<CineBookDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<CineBookDbContext>());

        services.Configure<BookingOptions>(config.GetSection(BookingOptions.SectionName));
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.Configure<QrOptions>(config.GetSection(QrOptions.SectionName));
        services.Configure<MockGatewayOptions>(config.GetSection(MockGatewayOptions.SectionName));
        services.Configure<VnPayOptions>(config.GetSection(VnPayOptions.SectionName));

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddSingleton<IQrSigner, HmacQrSigner>();
        services.AddSingleton<IEmailSender, LoggingEmailSender>();

        // Một interface, hai cài đặt — chọn cổng nào là việc của cấu hình và của khách,
        // tầng Application không biết cổng nào đang chạy (nguyên lý đảo ngược phụ thuộc).
        services.AddHttpClient();
        services.AddSingleton<MockPaymentGateway>();
        services.AddSingleton<VnPayGateway>();
        services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<MockPaymentGateway>());
        services.AddSingleton<IPaymentGateway>(sp => sp.GetRequiredService<VnPayGateway>());

        services.AddScoped<AuthService>();
        services.AddScoped<PricingService>();
        services.AddScoped<RoomService>();
        services.AddScoped<ShowtimeService>();
        services.AddScoped<PromotionService>();
        services.AddScoped<BookingService>();
        services.AddScoped<PaymentService>();
        services.AddScoped<RefundService>();
        services.AddScoped<TicketService>();
        services.AddScoped<CheckInService>();
        services.AddScoped<ReportService>();
        services.AddScoped<DbSeeder>();

        services.AddHostedService<ExpireBookingsService>();
        services.AddHostedService<KeepAwakeService>();

        return services;
    }
}
