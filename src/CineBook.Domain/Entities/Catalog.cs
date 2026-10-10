using CineBook.Domain.Common;
using CineBook.Domain.Enums;

namespace CineBook.Domain.Entities;

public class Combo : BaseEntity
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Promotion : BaseEntity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public DiscountType DiscountType { get; set; }
    public decimal Value { get; set; }
    public decimal? MaxDiscount { get; set; }
    public decimal MinOrderAmount { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public int PerUserLimit { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public class SavedVoucher
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid PromotionId { get; set; }
    public Promotion Promotion { get; set; } = null!;
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
}

public class PriceRule : BaseEntity
{
    public string Name { get; set; } = null!;
    public Guid? CinemaId { get; set; }
    public RoomType? RoomType { get; set; }
    public SeatType? SeatType { get; set; }

    public int? DayOfWeek { get; set; }

    public TimeOnly? TimeFrom { get; set; }
    public TimeOnly? TimeTo { get; set; }

    public decimal Multiplier { get; set; } = 1m;
    public decimal Surcharge { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AuditLog : BaseEntity
{
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public string? EntityId { get; set; }
    public string? DataBefore { get; set; }
    public string? DataAfter { get; set; }
    public string? Note { get; set; }
}
