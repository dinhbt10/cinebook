using System.Text.RegularExpressions;
using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Application.Dtos;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Application.Services;

/// <summary>
/// Kho voucher của khách, việc thẩm định điều kiện dùng, và phần quản trị voucher.
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

        // Đơn chờ đã quá hạn giữ ghế không còn chiếm lượt dùng, kể cả khi job dọn đơn chưa chạy.
        var usedByUser = await db.Bookings.CountAsync(b =>
            b.PromotionId == promo.Id && b.UserId == userId &&
            (b.Status == BookingStatus.Paid ||
             (b.Status == BookingStatus.Pending && b.HoldExpiresAtUtc > now)), ct);

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

    // ---------------------------------------------------------------- quản trị

    /// <summary>
    /// Mã chỉ gồm chữ hoa không dấu, số, gạch ngang, gạch dưới. Phải lưu chữ hoa vì lúc
    /// áp mã, <see cref="BookingService"/> và <see cref="SaveByCodeAsync"/> đều viết hoa
    /// chuỗi khách nhập rồi so bằng — mã lưu chữ thường sẽ không bao giờ khớp.
    /// Tối đa 32 ký tự theo độ dài cột.
    /// </summary>
    private static readonly Regex CodePattern = new("^[A-Z0-9_-]{3,32}$", RegexOptions.Compiled);

    /// <summary>Mọi voucher, kể cả đã tắt hoặc hết hạn, mới tạo xếp trên.</summary>
    public async Task<IReadOnlyList<AdminPromotionDto>> GetAllForAdminAsync(CancellationToken ct = default) =>
        await db.Promotions.AsNoTracking()
            .OrderByDescending(p => p.CreatedAtUtc)
            .Select(p => new AdminPromotionDto(
                p.Id, p.Code, p.Name, p.DiscountType.ToString(), p.Value, p.MaxDiscount,
                p.MinOrderAmount, p.StartsAtUtc, p.EndsAtUtc, p.UsageLimit, p.UsedCount,
                p.PerUserLimit, p.IsActive,
                db.Bookings.Any(b => b.PromotionId == p.Id)))
            .ToListAsync(ct);

    public async Task<Result<Guid>> CreateAsync(SavePromotionRequest req, CancellationToken ct = default)
    {
        var code = NormalizeCode(req.Code);
        var invalid = Validate(req, code);
        if (invalid is not null) return Result<Guid>.Fail(invalid.ErrorCode!, invalid.Error!);

        if (await db.Promotions.AnyAsync(p => p.Code == code, ct))
            return Result<Guid>.Fail("PROMO_CODE_TAKEN", $"Mã {code} đã tồn tại.");

        var promo = new Promotion { Code = code };
        Apply(promo, req);
        db.Promotions.Add(promo);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Ok(promo.Id);
    }

    public async Task<Result> UpdateAsync(Guid id, SavePromotionRequest req, CancellationToken ct = default)
    {
        var promo = await db.Promotions.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (promo is null) return Result.Fail("NOT_FOUND", "Không tìm thấy voucher.");

        var code = NormalizeCode(req.Code);
        var invalid = Validate(req, code);
        if (invalid is not null) return invalid;

        // Đã có đơn dùng thì khoá mã và mức giảm: khách đã nhớ mã, và hai đơn cùng
        // một voucher mà mức giảm khác nhau thì báo cáo không còn đối chiếu được.
        var changesTerms = code != promo.Code
                           || req.DiscountType != promo.DiscountType
                           || req.Value != promo.Value;
        if (changesTerms && await IsInUseAsync(id, ct))
            return Result.Fail("PROMO_LOCKED",
                "Voucher đã có đơn sử dụng nên không đổi được mã, loại giảm và giá trị.");

        if (req.UsageLimit is { } limit && limit < promo.UsedCount)
            return Result.Fail("PROMO_INVALID_INPUT",
                $"Tổng lượt không được nhỏ hơn số lượt đã dùng ({promo.UsedCount}).");

        if (code != promo.Code && await db.Promotions.AnyAsync(p => p.Code == code, ct))
            return Result.Fail("PROMO_CODE_TAKEN", $"Mã {code} đã tồn tại.");

        promo.Code = code;
        Apply(promo, req);
        promo.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    public async Task<Result> SetActiveAsync(Guid id, bool value, CancellationToken ct = default)
    {
        var promo = await db.Promotions.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (promo is null) return Result.Fail("NOT_FOUND", "Không tìm thấy voucher.");

        promo.IsActive = value;
        promo.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return Result.Ok();
    }

    /// <summary>
    /// Voucher đã có đơn dùng thì chỉ tắt, không xoá: khoá ngoại từ Booking để SetNull,
    /// xoá thật sẽ làm các đơn cũ mất dấu voucher đã áp. Cùng cách xử lý với phim đã có
    /// suất chiếu. Trả về true nếu đã xoá hẳn, false nếu chỉ chuyển sang ngừng áp dụng.
    /// </summary>
    public async Task<Result<bool>> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var promo = await db.Promotions.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (promo is null) return Result<bool>.Fail("NOT_FOUND", "Không tìm thấy voucher.");

        if (await IsInUseAsync(id, ct))
        {
            promo.IsActive = false;
            promo.UpdatedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return Result<bool>.Ok(false);
        }

        db.Promotions.Remove(promo);
        await db.SaveChangesAsync(ct);
        return Result<bool>.Ok(true);
    }

    private Task<bool> IsInUseAsync(Guid promotionId, CancellationToken ct) =>
        db.Bookings.AnyAsync(b => b.PromotionId == promotionId, ct);

    private static string NormalizeCode(string? code) => (code ?? "").Trim().ToUpperInvariant();

    /// <summary>Kiểm tra dữ liệu nhập, trả về null nếu hợp lệ.</summary>
    private static Result? Validate(SavePromotionRequest r, string code)
    {
        if (!CodePattern.IsMatch(code))
            return Invalid("Mã dài 3–32 ký tự, chỉ gồm chữ không dấu, số, gạch ngang hoặc gạch dưới.");
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 128)
            return Invalid("Tên voucher không được để trống và tối đa 128 ký tự.");
        if (!Enum.IsDefined(r.DiscountType))
            return Invalid("Loại giảm không hợp lệ.");
        if (r.DiscountType == DiscountType.Percentage && (r.Value <= 0 || r.Value > 100))
            return Invalid("Phần trăm giảm phải lớn hơn 0 và không quá 100.");
        if (r.DiscountType == DiscountType.FixedAmount && r.Value <= 0)
            return Invalid("Số tiền giảm phải lớn hơn 0.");
        if (r.MaxDiscount is <= 0)
            return Invalid("Mức giảm tối đa phải lớn hơn 0, để trống nếu không giới hạn.");
        if (r.MinOrderAmount < 0)
            return Invalid("Giá trị đơn tối thiểu không được âm.");
        if (ToUtc(r.EndsAtUtc) <= ToUtc(r.StartsAtUtc))
            return Invalid("Thời gian kết thúc phải sau thời gian bắt đầu.");
        if (r.UsageLimit is < 1)
            return Invalid("Tổng lượt dùng phải từ 1 trở lên, để trống nếu không giới hạn.");
        if (r.PerUserLimit < 1)
            return Invalid("Số lượt mỗi khách phải từ 1 trở lên.");
        return null;

        static Result Invalid(string message) => Result.Fail("PROMO_INVALID_INPUT", message);
    }

    private static void Apply(Promotion p, SavePromotionRequest r)
    {
        p.Name = r.Name.Trim();
        p.DiscountType = r.DiscountType;
        p.Value = r.Value;
        // Trần giảm chỉ có nghĩa với giảm theo phần trăm.
        p.MaxDiscount = r.DiscountType == DiscountType.Percentage ? r.MaxDiscount : null;
        p.MinOrderAmount = r.MinOrderAmount;
        p.StartsAtUtc = ToUtc(r.StartsAtUtc);
        p.EndsAtUtc = ToUtc(r.EndsAtUtc);
        p.UsageLimit = r.UsageLimit;
        p.PerUserLimit = r.PerUserLimit;
        p.IsActive = r.IsActive;
    }

    /// <summary>
    /// Npgsql từ chối ghi DateTime không mang Kind = Utc vào cột timestamptz. Giao diện
    /// luôn gửi chuỗi có hậu tố Z; gọi thẳng API mà thiếu múi giờ thì coi như đã là UTC.
    /// </summary>
    private static DateTime ToUtc(DateTime d) => d.Kind switch
    {
        DateTimeKind.Utc => d,
        DateTimeKind.Local => d.ToUniversalTime(),
        _ => DateTime.SpecifyKind(d, DateTimeKind.Utc),
    };
}
