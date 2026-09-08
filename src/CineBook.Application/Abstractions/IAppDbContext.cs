using CineBook.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CineBook.Application.Abstractions;

/// <summary>
/// Cho phép tầng Application làm việc với cơ sở dữ liệu mà không phụ thuộc
/// vào lớp DbContext cụ thể ở tầng Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Movie> Movies { get; }
    DbSet<Genre> Genres { get; }
    DbSet<MovieGenre> MovieGenres { get; }
    DbSet<Cinema> Cinemas { get; }
    DbSet<Room> Rooms { get; }
    DbSet<Seat> Seats { get; }
    DbSet<Showtime> Showtimes { get; }
    DbSet<ShowtimeSeat> ShowtimeSeats { get; }
    DbSet<Booking> Bookings { get; }
    DbSet<BookingCombo> BookingCombos { get; }
    DbSet<Ticket> Tickets { get; }
    DbSet<Payment> Payments { get; }
    DbSet<Combo> Combos { get; }
    DbSet<Promotion> Promotions { get; }
    DbSet<SavedVoucher> SavedVouchers { get; }
    DbSet<PriceRule> PriceRules { get; }
    DbSet<AuditLog> AuditLogs { get; }

    DatabaseFacade Database { get; }
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
