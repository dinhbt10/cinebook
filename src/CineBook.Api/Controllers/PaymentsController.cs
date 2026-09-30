using System.Globalization;
using System.Net;
using CineBook.Api.Common;
using CineBook.Application.Dtos;
using CineBook.Application.Services;
using CineBook.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Api.Controllers;

[ApiController]
[Route("api")]
public class PaymentsController(
    PaymentService payments,
    VnPayGateway vnpay,
    ILogger<PaymentsController> logger) : ControllerBase
{
    [HttpPost("bookings/{bookingId:guid}/payments")]
    [Authorize]
    public async Task<IActionResult> Create(
        Guid bookingId, CreatePaymentRequest req, CancellationToken ct) =>
        (await payments.CreateAsync(bookingId, User.UserId(), req, ct)).ToActionResult();

    /// <summary>
    /// Điểm nhận webhook của cổng thanh toán. Đây là nguồn sự thật duy nhất về
    /// việc khách đã trả tiền hay chưa (BR-07). Luôn trả 200 khi đã xử lý trước đó
    /// để cổng không gửi lại mãi.
    /// </summary>
    [HttpPost("payments/webhook/{provider}")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(string provider, CancellationToken ct)
    {
        var payload = new Dictionary<string, string>();
        foreach (var kv in Request.Query) payload[kv.Key] = kv.Value.ToString();
        if (Request.HasFormContentType)
            foreach (var kv in Request.Form) payload[kv.Key] = kv.Value.ToString();

        var result = await payments.HandleCallbackAsync(provider, payload, ct);
        return result.Succeeded
            ? Ok(new { message = result.Value })
            : result.ToActionResult();
    }

    /// <summary>Các cổng thanh toán máy chủ đang bật, để giao diện cho khách chọn.</summary>
    [HttpGet("payments/providers")]
    [AllowAnonymous]
    public IActionResult Providers() => Ok(payments.AvailableProviders());

    // ---------------------------------------------------------------- VNPay

    /// <summary>
    /// Đường VNPay đưa khách quay lại sau khi rời cổng.
    ///
    /// Theo đúng lý thuyết thì đường này chỉ để hiển thị, còn IPN mới là nguồn sự thật (BR-07).
    /// Nhưng máy chủ VNPay không gọi được IPN vào máy chạy localhost, nên ở môi trường
    /// phát triển đường này cũng ghi nhận thanh toán. An toàn vì chữ ký vẫn được kiểm tra
    /// và việc ghi nhận là idempotent (BR-08) — IPN về sau chỉ nhận "đã xử lý trước đó".
    /// </summary>
    [HttpGet("payments/vnpay-return")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayReturn(CancellationToken ct)
    {
        var payload = QueryToDictionary();
        var appBase = vnpay.AppBaseUrl;

        if (!payload.TryGetValue("vnp_TxnRef", out var txnRef) || string.IsNullOrWhiteSpace(txnRef))
            return Redirect($"{appBase}/");

        var bookingId = await payments.FindBookingIdByTxnAsync(vnpay.Provider, txnRef, ct);
        if (bookingId is null) return Redirect($"{appBase}/");

        var result = await payments.HandleCallbackAsync(vnpay.Provider, payload, ct);
        var code = payload.GetValueOrDefault("vnp_ResponseCode", "99");

        if (!result.Succeeded)
            logger.LogWarning("Đường quay lại VNPay không ghi nhận được giao dịch {Txn}: {Code}",
                txnRef, result.ErrorCode);

        // Trang kết quả tự hỏi lại trạng thái đơn, chỉ cần mã lỗi để hiện thông báo cho đúng.
        return Redirect($"{appBase}/dat-ve/ket-qua/{bookingId}?vnp_ResponseCode={Uri.EscapeDataString(code)}");
    }

    /// <summary>
    /// IPN — máy chủ VNPay gọi thẳng vào đây, không qua trình duyệt khách.
    /// Bắt buộc trả về đúng khuôn dạng RspCode của VNPay, nếu không cổng sẽ gọi lại nhiều lần.
    /// Chỉ chạy được khi hệ thống có địa chỉ công khai (deploy thật hoặc ngrok).
    /// </summary>
    [HttpGet("payments/vnpay-ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayIpn(CancellationToken ct)
    {
        var payload = QueryToDictionary();

        if (!vnpay.VerifySignature(payload))
            return Ok(new { RspCode = "97", Message = "Invalid signature" });

        var result = await payments.HandleCallbackAsync(vnpay.Provider, payload, ct);

        return Ok(result.ErrorCode switch
        {
            null => new { RspCode = "00", Message = "Confirm Success" },
            "TXN_NOT_FOUND" => new { RspCode = "01", Message = "Order not found" },
            "INVALID_STATE" => new { RspCode = "02", Message = "Order already confirmed" },
            "AMOUNT_MISMATCH" => new { RspCode = "04", Message = "Invalid amount" },
            "BAD_SIGNATURE" => new { RspCode = "97", Message = "Invalid signature" },
            _ => new { RspCode = "99", Message = "Unknown error" },
        });
    }

    private Dictionary<string, string> QueryToDictionary()
    {
        var payload = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in Request.Query) payload[kv.Key] = kv.Value.ToString();
        return payload;
    }
}

/// <summary>
/// Trang thanh toán giả lập. Giữ đúng đặc tính của cổng thật: kết quả về bằng
/// webhook có chữ ký, và có nút gửi webhook hai lần để chứng minh hệ thống chỉ
/// ghi nhận một lần (BR-08) — điều rất khó demo với cổng thanh toán thật.
/// </summary>
[ApiController]
[Route("mock-gateway")]
[AllowAnonymous]
public class MockGatewayController(
    MockPaymentGateway gateway,
    PaymentService payments) : ControllerBase
{
    [HttpGet]
    public ContentResult Page(
        [FromQuery] string txn, [FromQuery] string amount,
        [FromQuery] string code, [FromQuery] string returnUrl)
    {
        // Hiển thị tiền theo cách người Việt đọc: 155.000 đ
        var amountText = decimal.TryParse(amount, out var value) ? value.ToString("N0") : amount;

        // Cả ba giá trị đến thẳng từ query string. Trang này cùng nguồn với ứng dụng (token
        // nằm trong localStorage), nên chèn thô vào HTML là mở đường cho XSS lấy token.
        var codeHtml = WebUtility.HtmlEncode(code);
        var txnHtml = WebUtility.HtmlEncode(txn);
        var amountHtml = WebUtility.HtmlEncode(amountText);

        // returnUrl được gán vào location.href: chỉ nhận đường dẫn cùng nguồn, chặn
        // "javascript:…" và việc dẫn khách sang trang lạ sau khi thanh toán.
        var safeReturnUrl = IsSameOrigin(returnUrl) ? returnUrl : "";

        var html = $$"""
            <!doctype html><html lang="vi"><head><meta charset="utf-8">
            <meta name="viewport" content="width=device-width,initial-scale=1">
            <title>Cổng thanh toán giả lập</title>
            <style>
              body{font-family:system-ui,sans-serif;background:#0f1115;color:#e8e6e3;
                   display:flex;align-items:center;justify-content:center;min-height:100vh;margin:0;padding:24px}
              .card{background:#1a1d23;border:1px solid #2a2f38;border-radius:12px;padding:28px;max-width:440px;width:100%}
              h1{font-size:18px;margin:0 0 4px}
              .sub{color:#8b939f;font-size:13px;margin:0 0 20px}
              dl{display:grid;grid-template-columns:auto 1fr;gap:8px 16px;margin:0 0 22px;font-size:14px}
              dt{color:#8b939f}
              dd{margin:0;text-align:right;font-variant-numeric:tabular-nums}
              button{width:100%;padding:11px;border-radius:8px;border:1px solid #2a2f38;font-size:14px;
                     cursor:pointer;margin-bottom:9px;background:#22262e;color:#e8e6e3}
              .ok{background:#166534;border-color:#166534;color:#fff;font-weight:600}
              .fail{background:#3a1d1d;border-color:#5b2626;color:#f4b8b8}
              #log{margin-top:16px;font-size:12.5px;color:#8b939f;white-space:pre-wrap;line-height:1.6}
            </style></head><body><div class="card">
              <h1>Cổng thanh toán giả lập</h1>
              <p class="sub">Mô phỏng VNPay / MoMo cho môi trường học tập</p>
              <dl>
                <dt>Mã đặt vé</dt><dd>{{codeHtml}}</dd>
                <dt>Mã giao dịch</dt><dd>{{txnHtml}}</dd>
                <dt>Số tiền</dt><dd><b>{{amountHtml}} đ</b></dd>
              </dl>
              <button class="ok"   onclick="send('success',1)">Thanh toán thành công</button>
              <button class="fail" onclick="send('failed',1)">Thanh toán thất bại</button>
              <button              onclick="send('success',2)">Gửi webhook 2 lần (thử chống trùng)</button>
              <div id="log"></div>
            <script>
              const txn = {{Json(txn)}}, ret = {{Json(safeReturnUrl)}}, amount = {{Json(amount)}};
              async function send(status, times){
                const log = document.getElementById('log'); log.textContent = '';
                for (let i = 1; i <= times; i++){
                  const r = await fetch(`/mock-gateway/notify?txn=${encodeURIComponent(txn)}&status=${status}&amount=${encodeURIComponent(amount)}`,
                                        {method:'POST'});
                  const body = await r.json().catch(() => ({}));
                  log.textContent += `Lần ${i}: HTTP ${r.status} — ${body.message ?? JSON.stringify(body)}\n`;
                }
                // Thất bại cũng đưa khách quay lại, kèm dấu hiệu để trang kết quả
                // cho thanh toán lại — nếu không khách kẹt ở trang cổng trong khi ghế vẫn đang giữ.
                if (ret) {
                  const target = status === 'success'
                    ? ret : ret + (ret.includes('?') ? '&' : '?') + 'payment=failed';
                  log.textContent += 'Đang quay lại website…';
                  setTimeout(() => location.href = target, times > 1 ? 1600 : 700);
                }
              }
            </script></div></body></html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>Đóng vai cổng thanh toán gọi webhook về hệ thống, có ký đầy đủ.</summary>
    [HttpPost("notify")]
    public async Task<IActionResult> Notify(
        [FromQuery] string txn, [FromQuery] string status, [FromQuery] string? amount, CancellationToken ct)
    {
        // Cổng thật luôn báo số tiền đã thu. Gọi thẳng không kèm số tiền (script kiểm thử)
        // thì báo đúng số tiền của giao dịch, như một cổng thu đủ tiền.
        amount ??= (await payments.FindAmountByTxnAsync(gateway.Provider, txn, ct))?
            .ToString("0", CultureInfo.InvariantCulture) ?? "0";

        var payload = new Dictionary<string, string>
        {
            ["txn"] = txn,
            ["status"] = status,
            ["amount"] = amount,
            ["at"] = DateTime.UtcNow.ToString("O")
        };
        payload["signature"] = gateway.ComputeSignature(payload);

        var result = await payments.HandleCallbackAsync(gateway.Provider, payload, ct);
        return result.Succeeded
            ? Ok(new { message = result.Value })
            : StatusCode(400, new { code = result.ErrorCode, message = result.Error });
    }

    private bool IsSameOrigin(string? url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        if (url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\")) return true;
        return Uri.TryCreate(url, UriKind.Absolute, out var u)
               && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)
               && string.Equals(u.Authority, Request.Host.Value, StringComparison.OrdinalIgnoreCase);
    }

    private static string Json(string value) =>
        System.Text.Json.JsonSerializer.Serialize(value);
}
