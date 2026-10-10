using CineBook.Domain.Common;
using CineBook.Domain.Enums;

namespace CineBook.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string? Phone { get; set; }
    public DateOnly? BirthDate { get; set; }
    public UserRole Role { get; set; } = UserRole.Customer;
    public bool EmailVerified { get; set; }
    public bool IsActive { get; set; } = true;

    public Guid? CinemaId { get; set; }
    public Cinema? Cinema { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
