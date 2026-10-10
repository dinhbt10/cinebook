using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CineBook.Infrastructure.Persistence;

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
