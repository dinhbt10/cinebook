using System.Text;
using System.Text.Json.Serialization;
using CineBook.Api.Common;
using CineBook.Application.Abstractions;
using CineBook.Infrastructure;
using CineBook.Infrastructure.Persistence;
using CineBook.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Render (và hầu hết PaaS) tiêm cổng qua biến PORT lúc chạy, mỗi lần deploy một số khác.
// Không đọc biến này thì tiến trình nghe nhầm cổng và nền tảng coi như deploy thất bại.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://+:{port}");

builder.Services.AddCineBook(builder.Configuration);

// Cổng thanh toán thật cần biết IP của khách và địa chỉ gốc của site.
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IRequestContext, HttpRequestContext>();

builder.Services.AddControllers().AddJsonOptions(o =>
{
    // Enum trả về dạng chữ để frontend đọc được ngay: "Held", "Paid"…
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

// ---------------- xác thực ----------------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

// ---------------- Swagger ----------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "CineBook API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Dán access token nhận được từ /api/auth/login"
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        }] = Array.Empty<string>()
    });
});

// Khi giao diện nằm cùng tên miền (chạy gộp ở máy, hoặc qua rewrite của Vercel)
// thì trình duyệt coi là cùng nguồn nên không cần CORS. Danh sách này để dành cho
// trường hợp gọi thẳng vào API từ một tên miền khác.
// Cổng 5111 là cổng cố định của giao diện dự án này (xem web/vite.config.ts).
const string DevCors = "dev";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5111"];

builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// ---------------- migration + dữ liệu mẫu ----------------
if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CineBookDbContext>();
    await db.Database.MigrateAsync();

    if (app.Configuration.GetValue("Database:Seed", true))
        await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

// Mặc định Swagger chỉ bật lúc dev. Đặt Swagger:Enabled = true để bật trên bản deploy
// khi cần trình bày API lúc bảo vệ, rồi tắt lại sau.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "CineBook API v1"));
}

app.UseCors(DevCors);

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Render gọi đường dẫn này để biết tiến trình đã sống chưa. Cố ý KHÔNG chạm cơ sở
// dữ liệu: kiểm tra sống phải trả lời nhanh và không đánh thức Neon một cách vô ích.
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    utc = DateTime.UtcNow,
})).AllowAnonymous();

// Đường dẫn /api/... không khớp controller nào phải trả 404 JSON.
// Nếu để rơi vào fallback bên dưới, gọi sai endpoint sẽ nhận về trang HTML kèm mã 200
// và lỗi chỉ lộ ra ở giao diện dưới dạng khó hiểu ("providers.map is not a function").
app.MapFallback("/api/{**rest}", () => Results.NotFound(new
{
    code = "ENDPOINT_NOT_FOUND",
    message = "Không tìm thấy endpoint này. Máy chủ có thể đang chạy bản build cũ.",
}));

// Mọi đường dẫn còn lại trả về index.html để React Router tự định tuyến.
// Khi deploy tách đôi (giao diện nằm trên Vercel), wwwroot rỗng nên không có index.html
// để phục vụ — lúc đó trả lời rõ ràng thay vì để trình duyệt nhận trang trống khó hiểu.
if (File.Exists(Path.Combine(app.Environment.WebRootPath ?? "wwwroot", "index.html")))
{
    app.MapFallbackToFile("index.html");
}
else
{
    app.MapFallback(() => Results.Text(
        "CineBook API đang chạy. Giao diện được phục vụ ở tên miền riêng.",
        "text/plain; charset=utf-8"));
}

app.Run();
