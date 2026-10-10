using CineBook.Application.Abstractions;
using CineBook.Application.Common;
using CineBook.Application.Dtos;
using CineBook.Domain.Entities;
using CineBook.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CineBook.Application.Services;

public class PaymentService(
    IAppDbContext db,
    IEnumerable<IPaymentGateway> gateways,
    BookingService bookings,
    IDateTimeProvider clock,
    ILogger<PaymentService> logger)
{
    private IPaymentGateway? Gateway(string provider) =>
        gateways.FirstOrDefault(g => g.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<PaymentProviderDto> AvailableProviders() =>
        gateways.Where(g => g.IsConfigured)
                .Select(g => new PaymentProviderDto(g.Provider, g.DisplayName,
                    g.Methods.Select(m => new PaymentMethodDto(m.Code, m.DisplayName)).ToList()))
                .ToList();

    public Task<Guid?> FindBookingIdByTxnAsync(
        string provider, string providerTxnId, CancellationToken ct = default) =>
        db.Payments.AsNoTracking()
            .Where(p => p.Provider == provider && p.ProviderTxnId == providerTxnId)
            .Select(p => (Guid?)p.BookingId)
            .FirstOrDefaultAsync(ct);

    public async Task<Result<CreatePaymentResponse>> CreateAsync(
        Guid bookingId, Guid userId, CreatePaymentRequest req, CancellationToken ct = default)
    {
        var gateway = Gateway(req.Provider);
        if (gateway is null)
            return Result<CreatePaymentResponse>.Fail("PROVIDER_UNKNOWN", "Cổng thanh toán không được hỗ trợ.");
        if (!gateway.IsConfigured)
            return Result<CreatePaymentResponse>.Fail("PROVIDER_NOT_CONFIGURED",
                $"Cổng {gateway.DisplayName} chưa được cấu hình trên máy chủ.");

        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId && b.UserId == userId, ct);
        if (booking is null)
            return Result<CreatePaymentResponse>.Fail("NOT_FOUND", "Không tìm thấy đơn hàng.");
        if (booking.Status != BookingStatus.Pending)
            return Result<CreatePaymentResponse>.Fail("INVALID_STATE", "Đơn hàng không ở trạng thái chờ thanh toán.");
        if (booking.HoldExpiresAtUtc <= clock.UtcNow)
            return Result<CreatePaymentResponse>.Fail("HOLD_EXPIRED",
                "Đã hết thời gian giữ ghế. Vui lòng chọn lại ghế.");

        var intent = await gateway.CreateAsync(
            booking, new PaymentIntentRequest(req.ReturnUrl, req.MethodCode), ct);

        var payment = new Payment
        {
            BookingId = booking.Id,
            Provider = gateway.Provider,
            ProviderTxnId = intent.ProviderTxnId,
            Amount = booking.Total,
            Status = PaymentStatus.Pending
        };
        db.Payments.Add(payment);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Trùng mã giao dịch {Txn} của cổng {Provider}",
                intent.ProviderTxnId, gateway.Provider);
            return Result<CreatePaymentResponse>.Fail("TXN_DUPLICATE",
                "Không tạo được giao dịch, vui lòng bấm thanh toán lại.");
        }

        return Result<CreatePaymentResponse>.Ok(new CreatePaymentResponse(
            payment.Id, gateway.Provider, intent.ProviderTxnId, intent.PaymentUrl));
    }

    public async Task<Result<string>> HandleCallbackAsync(
        string provider, IDictionary<string, string> payload, CancellationToken ct = default)
    {
        var gateway = Gateway(provider);
        if (gateway is null) return Result<string>.Fail("PROVIDER_UNKNOWN", "Cổng thanh toán không hợp lệ.");

        if (!gateway.VerifySignature(payload))
        {
            logger.LogWarning("Webhook {Provider} có chữ ký không hợp lệ", provider);
            return Result<string>.Fail("BAD_SIGNATURE", "Chữ ký không hợp lệ.");
        }

        var callback = gateway.ParseCallback(payload);

        var payment = await db.Payments
            .FirstOrDefaultAsync(p => p.Provider == gateway.Provider
                                   && p.ProviderTxnId == callback.ProviderTxnId, ct);

        if (payment is null)
            return Result<string>.Fail("TXN_NOT_FOUND", "Không tìm thấy giao dịch.");

        if (payment.Status != PaymentStatus.Pending)
        {
            logger.LogInformation("Bỏ qua webhook trùng cho giao dịch {Txn} (đã ở trạng thái {Status})",
                callback.ProviderTxnId, payment.Status);
            return Result<string>.Ok("Đã xử lý trước đó, bỏ qua.");
        }

        payment.RawPayload = callback.RawPayload;
        payment.CompletedAtUtc = clock.UtcNow;

        if (!callback.Succeeded)
        {
            payment.Status = PaymentStatus.Failed;
            await db.SaveChangesAsync(ct);
            return Result<string>.Ok("Ghi nhận thanh toán thất bại.");
        }

        if (callback.Amount != payment.Amount)
        {
            payment.Status = PaymentStatus.Failed;
            logger.LogError("Số tiền cổng báo {Paid} khác số tiền giao dịch {Expected} ({Txn})",
                callback.Amount, payment.Amount, callback.ProviderTxnId);
            await db.SaveChangesAsync(ct);
            return Result<string>.Fail("AMOUNT_MISMATCH", "Số tiền thanh toán không khớp với đơn hàng.");
        }

        payment.Status = PaymentStatus.Succeeded;

        var paidByOther = await db.Payments.AnyAsync(p => p.BookingId == payment.BookingId
            && p.Id != payment.Id && p.Status == PaymentStatus.Succeeded, ct);

        var confirm = paidByOther
            ? Result<Booking>.Fail("ALREADY_PAID", "Đơn hàng đã được thanh toán bằng giao dịch khác.")
            : await bookings.ConfirmPaidAsync(payment.BookingId, ct);

        if (!confirm.Succeeded)
        {
            logger.LogError("Thanh toán thành công nhưng không chốt được đơn {BookingId}: {Error}",
                payment.BookingId, confirm.Error);
            return await AutoRefundAsync(payment, gateway, keepBookingStatus: paidByOther,
                confirm.Error!, ct);
        }

        await db.SaveChangesAsync(ct);
        return Result<string>.Ok("Đã ghi nhận thanh toán.");
    }

    private async Task<Result<string>> AutoRefundAsync(
        Payment payment, IPaymentGateway gateway, bool keepBookingStatus, string why, CancellationToken ct)
    {
        var ok = await gateway.RefundAsync(payment, payment.Amount, ct);

        db.AuditLogs.Add(new AuditLog
        {
            Action = ok ? "AUTO_REFUND" : "REFUND_REQUIRED",
            EntityType = nameof(Payment),
            EntityId = payment.Id.ToString(),
            Note = $"{payment.Amount:N0}đ — {why}"
        });

        if (!ok)
        {
            await db.SaveChangesAsync(ct);
            return Result<string>.Fail("REFUND_REQUIRED",
                "Thanh toán thành công nhưng không giao được vé, cần hoàn tiền thủ công.");
        }

        payment.Status = PaymentStatus.Refunded;
        payment.RefundedAmount = payment.Amount;

        if (!keepBookingStatus)
        {
            var booking = await db.Bookings.FirstAsync(b => b.Id == payment.BookingId, ct);
            booking.Status = BookingStatus.Refunded;
            booking.CancelledAtUtc = clock.UtcNow;
            booking.CancelReason = "Thanh toán về sau khi hết thời gian giữ ghế — đã hoàn tiền tự động.";
            await bookings.ReleaseSeatsOfBookingAsync(booking.Id, ct);
        }

        await db.SaveChangesAsync(ct);
        return Result<string>.Ok("Không giao được vé nên đã hoàn tiền cho khách.");
    }

    public Task<decimal?> FindAmountByTxnAsync(
        string provider, string providerTxnId, CancellationToken ct = default) =>
        db.Payments.AsNoTracking()
            .Where(p => p.Provider == provider && p.ProviderTxnId == providerTxnId)
            .Select(p => (decimal?)p.Amount)
            .FirstOrDefaultAsync(ct);

    public async Task<Result<decimal>> RefundAsync(
        Guid bookingId, decimal rate, string reason, Guid? actorUserId, CancellationToken ct = default)
    {
        var payment = await db.Payments
            .Where(p => p.BookingId == bookingId && p.Status == PaymentStatus.Succeeded)
            .FirstOrDefaultAsync(ct);

        if (payment is null) return Result<decimal>.Fail("NO_PAYMENT", "Đơn hàng chưa có giao dịch thành công.");

        var gateway = Gateway(payment.Provider);
        if (gateway is null) return Result<decimal>.Fail("PROVIDER_UNKNOWN", "Không tìm thấy cổng thanh toán.");

        var amount = PricingService.RoundToThousand(payment.Amount * rate);
        var ok = await gateway.RefundAsync(payment, amount, ct);
        if (!ok) return Result<decimal>.Fail("REFUND_FAILED", "Cổng thanh toán từ chối hoàn tiền.");

        payment.Status = PaymentStatus.Refunded;
        payment.RefundedAmount = amount;

        db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = "REFUND",
            EntityType = nameof(Booking),
            EntityId = bookingId.ToString(),
            Note = $"Hoàn {amount:N0}đ ({rate:P0}) — {reason}"
        });

        await db.SaveChangesAsync(ct);
        return Result<decimal>.Ok(amount);
    }
}
