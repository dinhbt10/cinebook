using CineBook.Application.Abstractions;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Infrastructure.Persistence;

public class CineBookDbContext(DbContextOptions<CineBookDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<MovieGenre> MovieGenres => Set<MovieGenre>();
    public DbSet<Cinema> Cinemas => Set<Cinema>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Showtime> Showtimes => Set<Showtime>();
    public DbSet<ShowtimeSeat> ShowtimeSeats => Set<ShowtimeSeat>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingCombo> BookingCombos => Set<BookingCombo>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Combo> Combos => Set<Combo>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<SavedVoucher> SavedVouchers => Set<SavedVoucher>();
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => base.SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder b)
    {
        // ---------- Người dùng ----------
        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256);
            e.Property(x => x.FullName).HasMaxLength(128);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.HasOne(x => x.Cinema).WithMany().HasForeignKey(x => x.CinemaId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        // ---------- Phim ----------
        b.Entity<Movie>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(256);
            e.HasIndex(x => x.Status);
        });
        b.Entity<Genre>(e => e.HasIndex(x => x.Slug).IsUnique());
        b.Entity<MovieGenre>(e =>
        {
            e.HasKey(x => new { x.MovieId, x.GenreId });
            e.HasOne(x => x.Movie).WithMany(m => m.MovieGenres).HasForeignKey(x => x.MovieId);
            e.HasOne(x => x.Genre).WithMany(g => g.MovieGenres).HasForeignKey(x => x.GenreId);
        });

        // ---------- Rạp, phòng, ghế ----------
        b.Entity<Room>(e =>
            e.HasOne(x => x.Cinema).WithMany(c => c.Rooms).HasForeignKey(x => x.CinemaId));

        b.Entity<Seat>(e =>
        {
            e.HasOne(x => x.Room).WithMany(r => r.Seats).HasForeignKey(x => x.RoomId);
            e.Property(x => x.RowLabel).HasMaxLength(4);
            e.HasIndex(x => new { x.RoomId, x.RowLabel, x.Number }).IsUnique();
            e.Ignore(x => x.Label);
        });

        // ---------- Suất chiếu ----------
        b.Entity<Showtime>(e =>
        {
            e.HasOne(x => x.Movie).WithMany(m => m.Showtimes).HasForeignKey(x => x.MovieId);
            e.HasOne(x => x.Room).WithMany(r => r.Showtimes).HasForeignKey(x => x.RoomId);
            e.Property(x => x.BasePrice).HasPrecision(12, 2);
            // BR-16: dùng để kiểm tra hai suất chồng giờ trong cùng một phòng.
            e.HasIndex(x => new { x.RoomId, x.StartsAtUtc });
            e.HasIndex(x => new { x.MovieId, x.StartsAtUtc });
        });

        // ---------- Ghế theo suất: trái tim của việc giữ ghế ----------
        b.Entity<ShowtimeSeat>(e =>
        {
            // BR-01: khoá chính ghép đảm bảo một ghế chỉ có đúng một dòng trong một suất.
            e.HasKey(x => new { x.ShowtimeId, x.SeatId });
            e.HasOne(x => x.Showtime).WithMany(s => s.Seats).HasForeignKey(x => x.ShowtimeId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Seat).WithMany().HasForeignKey(x => x.SeatId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Booking).WithMany(bk => bk.BookedSeats).HasForeignKey(x => x.BookingId)
             .OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.Price).HasPrecision(12, 2);

            // Truy vấn sơ đồ ghế và câu UPDATE giành ghế đều lọc theo hai cột này.
            e.HasIndex(x => new { x.ShowtimeId, x.Status });
            e.HasIndex(x => x.HoldExpiresAtUtc);

            // xmin của PostgreSQL làm dấu kiểm tra tranh chấp ghi.
            e.Property(x => x.Version)
             .HasColumnName("xmin").HasColumnType("xid")
             .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        });

        // ---------- Đơn hàng ----------
        b.Entity<Booking>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(16);
            e.HasOne(x => x.User).WithMany(u => u.Bookings).HasForeignKey(x => x.UserId);
            e.HasOne(x => x.Showtime).WithMany().HasForeignKey(x => x.ShowtimeId);
            e.HasOne(x => x.Promotion).WithMany().HasForeignKey(x => x.PromotionId)
             .OnDelete(DeleteBehavior.SetNull);
            foreach (var p in new[] { nameof(Booking.SeatsSubtotal), nameof(Booking.ConcessionSubtotal),
                                      nameof(Booking.DiscountAmount), nameof(Booking.Total) })
                e.Property(p).HasPrecision(12, 2);
            e.HasIndex(x => new { x.Status, x.HoldExpiresAtUtc });
            e.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
        });

        b.Entity<BookingCombo>(e =>
        {
            e.HasKey(x => new { x.BookingId, x.ComboId });
            e.HasOne(x => x.Booking).WithMany(bk => bk.Combos).HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.Combo).WithMany().HasForeignKey(x => x.ComboId);
            e.Property(x => x.UnitPrice).HasPrecision(12, 2);
        });

        // ---------- Vé ----------
        b.Entity<Ticket>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(16);
            e.HasOne(x => x.Booking).WithMany(bk => bk.Tickets).HasForeignKey(x => x.BookingId);
            e.HasOne(x => x.Seat).WithMany().HasForeignKey(x => x.SeatId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------- Thanh toán ----------
        b.Entity<Payment>(e =>
        {
            e.HasOne(x => x.Booking).WithMany(bk => bk.Payments).HasForeignKey(x => x.BookingId);
            e.Property(x => x.Amount).HasPrecision(12, 2);
            e.Property(x => x.RefundedAmount).HasPrecision(12, 2);
            e.Property(x => x.Provider).HasMaxLength(32);
            e.Property(x => x.ProviderTxnId).HasMaxLength(128);
            // BR-08: cùng một mã giao dịch của cổng chỉ được ghi nhận một lần.
            e.HasIndex(x => new { x.Provider, x.ProviderTxnId }).IsUnique();
        });

        // ---------- Bắp nước, khuyến mãi, giá ----------
        b.Entity<Combo>(e => e.Property(x => x.Price).HasPrecision(12, 2));
        b.Entity<Promotion>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(32);
            e.Property(x => x.Value).HasPrecision(12, 2);
            e.Property(x => x.MaxDiscount).HasPrecision(12, 2);
            e.Property(x => x.MinOrderAmount).HasPrecision(12, 2);
        });
        // Kho voucher của khách: mỗi người lưu một voucher tối đa một lần.
        b.Entity<SavedVoucher>(e =>
        {
            e.HasKey(x => new { x.UserId, x.PromotionId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Promotion).WithMany().HasForeignKey(x => x.PromotionId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.SavedAtUtc });
        });

        b.Entity<PriceRule>(e =>
        {
            e.Property(x => x.Multiplier).HasPrecision(6, 3);
            e.Property(x => x.Surcharge).HasPrecision(12, 2);
        });

        b.Entity<AuditLog>(e => e.HasIndex(x => new { x.EntityType, x.EntityId }));

        base.OnModelCreating(b);
    }
}
