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

/// <summary>
/// Voucher khách đã lưu vào kho của mình.
///
/// Trước đây khách phải nhớ và gõ đúng mã giảm giá. Bảng này cho phép lưu sẵn
/// voucher rồi tới bước thanh toán chỉ việc bấm chọn. Lưu vào kho KHÔNG giữ chỗ
/// lượt dùng — điều kiện dùng vẫn được kiểm tra lại lúc đặt vé (BR-05).
/// </summary>
public class SavedVoucher
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid PromotionId { get; set; }
    public Promotion Promotion { get; set; } = null!;
    public DateTime SavedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// BR-18: giá = giá cơ sở của suất × hệ số của các quy tắc khớp + phụ thu.
/// Trường nào để null nghĩa là quy tắc áp cho mọi giá trị của trường đó.
/// </summary>
public class PriceRule : BaseEntity
{
    public string Name { get; set; } = null!;
    public Guid? CinemaId { get; set; }
    public RoomType? RoomType { get; set; }
    public SeatType? SeatType { get; set; }

    /// <summary>0 = Chủ nhật … 6 = Thứ bảy. Null = mọi ngày.</summary>
    public int? DayOfWeek { get; set; }

    public TimeOnly? TimeFrom { get; set; }
    public TimeOnly? TimeTo { get; set; }

    public decimal Multiplier { get; set; } = 1m;
    public decimal Surcharge { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>BR-20: ghi lại mọi thao tác hoàn tiền và sửa giá.</summary>
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
