using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Application.Dtos;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Application.Services;

/// <summary>
/// Kho voucher của khách và việc thẩm định điều kiện dùng.
///
/// Toàn bộ luật xét voucher nằm ở <see cref="EvaluateAsync"/> và được dùng chung
/// cho hai nơi: giao diện hiện danh sách voucher kèm số tiền giảm, và
/// <see cref="BookingService"/> lúc thực sự chốt đơn. Nhờ vậy con số khách nhìn thấy
/// luôn khớp với con số hệ thống áp — không thể lệch nhau vì hai chỗ viết luật riêng.
/// </summary>
public class PromotionService(IAppDbContext db, IDateTimeProvider clock)
{
    /// <summary>Kết quả thẩm định một voucher cho một giá trị đơn cụ thể.</summary>
    public record Evaluation(bool Usable, string? ErrorCode, string? Reason, decimal Discount);

    // ---------------------------------------------------------------- thẩm định

    public async Task<Evaluation> EvaluateAsync(
        Promotion promo, Guid userId, decimal orderValue, CancellationToken ct = default)
    {
        var now = clock.UtcNow;

        if (!promo.IsActive)
            return new(false, "PROMO_INVALID", "Mã giảm giá đã ngừng áp dụng.", 0m);

        if (now < promo.StartsAtUtc)
            return new(false, "PROMO_NOT_STARTED", "Mã chưa tới ngày áp dụng.", 0m);

        if (now > promo.EndsAtUtc)
            return new(false, "PROMO_EXPIRED", "Mã giảm giá đã hết hạn.", 0m);

        if (promo.UsageLimit is { } limit && promo.UsedCount >= limit)
            return new(false, "PROMO_USED_UP", "Mã giảm giá đã hết lượt sử dụng.", 0m);

        var usedByUser = await db.Bookings.CountAsync(b =>
            b.PromotionId == promo.Id && b.UserId == userId &&
            (b.Status == BookingStatus.Paid || b.Status == BookingStatus.Pending), ct);

        if (usedByUser >= promo.PerUserLimit)
            return new(false, "PROMO_PER_USER", "Bạn đã dùng mã này rồi.", 0m);

        // Điều kiện đơn tối thiểu xét cuối cùng: đây là lý do khách còn "sửa được"
        // bằng cách mua thêm, nên thông báo phải nói rõ còn thiếu bao nhiêu.
        if (orderValue < promo.MinOrderAmount)
            return new(false, "PROMO_MIN_ORDER",
                $"Cần đơn từ {promo.MinOrderAmount:N0}đ (còn thiếu {promo.MinOrderAmount - orderValue:N0}đ).",
                0m);

        return new(true, null, null, Discount(promo, orderValue));
    }

    /// <summary>Số tiền giảm, đã chặn trần và làm tròn theo bội số nghìn đồng.</summary>
    public static decimal Discount(Promotion promo, decimal orderValue)
    {
        var discount = promo.DiscountType == DiscountType.Percentage
            ? orderValue * promo.Value / 100m
            : promo.Value;

        if (promo.MaxDiscount is { } max && discount > max) discount = max;
        if (discount > orderValue) discount = orderValue;

        return PricingService.RoundToThousand(discount);
    }

    // ---------------------------------------------------------------- kho voucher

    public async Task<Result> SaveAsync(Guid userId, Guid promotionId, CancellationToken ct = default)
    {
        var promo = await db.Promotions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == promotionId, ct);
        if (promo is null) return Result.Fail("NOT_FOUND", "Không tìm thấy voucher.");

        if (await db.SavedVouchers.AnyAsync(v => v.UserId == userId && v.PromotionId == promotionId, ct))
            return Result.Ok();   // lưu lại lần nữa cũng không sao

        db.SavedVouchers.Add(new SavedVoucher
        {
            UserId = userId,
            PromotionId = promotionId,
            SavedAtUtc = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    /// <summary>Lưu voucher bằng mã, dùng cho ô "nhập mã" — nhập đúng là vào thẳng kho.</summary>
    public async Task<Result<Guid>> SaveByCodeAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        var promo = await db.Promotions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == normalized, ct);

        if (promo is null) return Result<Guid>.Fail("PROMO_INVALID", "Mã giảm giá không tồn tại.");

        var saved = await SaveAsync(userId, promo.Id, ct);
        return saved.Succeeded
            ? Result<Guid>.Ok(promo.Id)
            : Result<Guid>.Fail(saved.ErrorCode!, saved.Error!);
    }

    public async Task<Result> RemoveAsync(Guid userId, Guid promotionId, CancellationToken ct = default)
    {
        var row = await db.SavedVouchers
            .FirstOrDefaultAsync(v => v.UserId == userId && v.PromotionId == promotionId, ct);
        if (row is null) return Result.Ok();

        db.SavedVouchers.Remove(row);
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    // ---------------------------------------------------------------- đọc

    /// <summary>
    /// Voucher để hiện ở bước đặt vé: gồm voucher trong kho và mọi voucher đang chạy,
    /// mỗi cái kèm số tiền giảm thật cho đơn này và lý do nếu chưa dùng được.
    /// Dùng được xếp trước, trong đó giảm nhiều xếp trên.
    /// </summary>
    public async Task<IReadOnlyList<VoucherDto>> GetForOrderAsync(
        Guid userId, decimal orderValue, bool savedOnly = false, CancellationToken ct = default)
    {
        var savedIds = await db.SavedVouchers.AsNoTracking()
            .Where(v => v.UserId == userId)
            .Select(v => v.PromotionId)
            .ToListAsync(ct);

        var query = db.Promotions.AsNoTracking();
        query = savedOnly
            ? query.Where(p => savedIds.Contains(p.Id))
            // Voucher đã lưu vẫn hiện dù đã tắt hoặc hết hạn, kèm lý do — để khách
            // hiểu vì sao nó không dùng được, thay vì thấy nó biến mất không dấu vết.
            : query.Where(p => p.IsActive || savedIds.Contains(p.Id));

        var promos = await query.ToListAsync(ct);

        var result = new List<VoucherDto>(promos.Count);
        foreach (var p in promos)
        {
            var e = await EvaluateAsync(p, userId, orderValue, ct);
            result.Add(new VoucherDto(
                p.Id, p.Code, p.Name, p.DiscountType.ToString(), p.Value, p.MaxDiscount,
                p.MinOrderAmount, p.EndsAtUtc,
                Saved: savedIds.Contains(p.Id),
                Usable: e.Usable,
                Reason: e.Reason,
                DiscountAmount: e.Usable ? e.Discount : Discount(p, orderValue)));
        }

        return result
            .OrderByDescending(v => v.Usable)
            .ThenByDescending(v => v.DiscountAmount)
            .ThenBy(v => v.EndsAtUtc)
            .ToList();
    }
}
