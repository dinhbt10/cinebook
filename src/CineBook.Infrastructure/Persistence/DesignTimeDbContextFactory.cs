using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CineBook.Infrastructure.Persistence;

/// <summary>
/// Chỉ dùng cho lệnh "dotnet ef" ở máy dev. Lấy chuỗi kết nối từ biến môi trường
/// CINEBOOK_DB; nếu không có thì dùng chuỗi giả — vẫn tạo được migration mà
/// không cần kết nối tới cơ sở dữ liệu thật.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CineBookDbContext>
{
    public CineBookDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("CINEBOOK_DB")
                   ?? "Host=localhost;Database=cinebook;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<CineBookDbContext>()
            .UseNpgsql(conn)
            .Options;

        return new CineBookDbContext(options);
    }
}
