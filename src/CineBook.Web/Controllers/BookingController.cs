using CineBook.Application.Dtos;
using CineBook.Application.Services;
using CineBook.Domain.Enums;
using CineBook.Infrastructure.Persistence;
using CineBook.Web.Common;
using CineBook.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CineBook.Web.Controllers;

[Authorize]
public class BookingController : Controller
{
    private const string SessionBookingKey = "BookingId";

    private readonly CineBookDbContext _context;
    private readonly BookingService _bookings;
    private readonly PaymentService _payments;
    private readonly PromotionService _promotions;

    public BookingController(CineBookDbContext context, BookingService bookings,
        PaymentService payments, PromotionService promotions)
    {
        _context = context;
        _bookings = bookings;
        _payments = payments;
        _promotions = promotions;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(Guid id)
    {
        var signedIn = User.Identity != null && User.Identity.IsAuthenticated;
        Guid? userId = signedIn ? User.UserId() : null;

        var seatMap = await _bookings.GetSeatMapAsync(id, userId);
        if (!seatMap.Succeeded)
            return View("NotFound");

        if (userId == null)
        {
            ViewBag.ShowtimeId = id;
            return View("LoginRequired");
        }

        var model = new BookingPageViewModel();
        model.SeatMap = seatMap.Value!;
        model.Combos = await LoadCombosAsync();
        model.Providers = _payments.AvailableProviders().ToList();
        model.AbortError = TempData["AbortError"] as string;
        model.Error = TempData["Error"] as string;

        var now = DateTime.UtcNow;
        var pendingId = await (from b in _context.Bookings
                               where b.UserId == userId
                                     && b.ShowtimeId == id
                                     && b.Status == BookingStatus.Pending
                                     && b.HoldExpiresAtUtc > now
                               orderby b.CreatedAtUtc descending
                               select (Guid?)b.Id).FirstOrDefaultAsync();

        var booking = pendingId == null ? null : await _bookings.GetForUserAsync(pendingId.Value, userId.Value);

        if (booking != null)
        {
            HttpContext.Session.SetString(SessionBookingKey, booking.Id.ToString());

            var heldSeats = TempData["HeldSeats"] as string;
            model.Booking = booking;
            model.Step = "confirm";
            model.Resumed = heldSeats == null;
            model.SelectedSeatIds = heldSeats == null
                ? booking.Seats.Select(s => s.SeatId).ToList()
                : ParseIds(heldSeats);
            if (!model.Resumed)
                model.ComboQuantities = booking.Combos.ToDictionary(c => c.ComboId, c => c.Quantity);
            model.Provider = TempData["Provider"] as string;
            model.Method = TempData["Method"] as string;
            return View(model);
        }

        HttpContext.Session.Remove(SessionBookingKey);

        var initSeats = TempData["InitSeats"] as string;
        var initCombos = TempData["InitCombos"] as string;
        if (initSeats != null)
            model.SelectedSeatIds = ParseIds(initSeats);
        if (initCombos != null)
            model.ComboQuantities = ParseCombos(initCombos);
        model.PromotionCode = TempData["InitPromo"] as string;
        model.Step = TempData["InitStep"] as string ?? "seats";
        return View(model);
    }

    public async Task<IActionResult> SeatMap(Guid id)
    {
        var result = await _bookings.GetSeatMapAsync(id, User.UserId());
        if (!result.Succeeded)
            return NotFound(new { message = result.Error });
        return Json(result.Value);
    }

    public async Task<IActionResult> Vouchers(decimal amount)
    {
        var vouchers = await _promotions.GetForOrderAsync(User.UserId(), amount);
        return Json(vouchers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVoucher(Guid id)
    {
        var result = await _promotions.SaveAsync(User.UserId(), id);
        return Json(new { ok = result.Succeeded, message = result.Error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnsaveVoucher(Guid id)
    {
        var result = await _promotions.RemoveAsync(User.UserId(), id);
        return Json(new { ok = result.Succeeded, message = result.Error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveVoucherByCode(string code)
    {
        var result = await _promotions.SaveByCodeAsync(User.UserId(), code ?? "");
        return Json(new { ok = result.Succeeded, message = result.Error });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hold(Guid id, HoldSeatsViewModel model)
    {
        var combos = model.Combos
            .Where(c => c.Quantity > 0)
            .Select(c => new ComboSelection(c.ComboId, c.Quantity))
            .ToList();
        var promotionCode = string.IsNullOrWhiteSpace(model.PromotionCode) ? null : model.PromotionCode.Trim();
        var request = new HoldSeatsRequest(id, model.SeatIds, combos, promotionCode);

        var result = await _bookings.HoldSeatsAsync(User.UserId(), request);
        if (result.Succeeded)
        {
            HttpContext.Session.SetString(SessionBookingKey, result.Value!.Id.ToString());
            TempData["HeldSeats"] = string.Join(",", model.SeatIds.Distinct());
            return RedirectToAction("Index", new { id });
        }

        TempData["Error"] = result.Error;
        TempData["InitCombos"] = FormatCombos(model.Combos);
        TempData["InitPromo"] = model.PromotionCode;
        if (result.ErrorCode == "SEATS_TAKEN")
        {
            TempData["InitStep"] = "seats";
        }
        else
        {
            TempData["InitStep"] = "extras";
            TempData["InitSeats"] = string.Join(",", model.SeatIds);
        }
        return RedirectToAction("Index", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(Guid id, PayViewModel model)
    {
        var bookingId = SessionBookingId();
        if (bookingId == null)
            return RedirectToAction("Index", new { id });

        var request = new CreatePaymentRequest(model.Provider, "/Booking/Result/" + bookingId, model.MethodCode ?? "");
        var result = await _payments.CreateAsync(bookingId.Value, User.UserId(), request);
        if (result.Succeeded)
            return Redirect(result.Value!.PaymentUrl);

        TempData["Error"] = result.Error;
        TempData["Provider"] = model.Provider;
        TempData["Method"] = model.MethodCode ?? "";
        TempData["HeldSeats"] = model.HeldSeats;
        return RedirectToAction("Index", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelHold(Guid id, PayViewModel model)
    {
        var bookingId = SessionBookingId();
        if (bookingId == null)
            return RedirectToAction("Index", new { id });

        var userId = User.UserId();
        var booking = await _bookings.GetForUserAsync(bookingId.Value, userId);
        var promotionCode = await (from b in _context.Bookings
                                   where b.Id == bookingId && b.Promotion != null
                                   select b.Promotion!.Code).FirstOrDefaultAsync();

        var result = await _bookings.CancelPendingAsync(bookingId.Value, userId);
        if (!result.Succeeded)
        {
            TempData["Error"] = result.Error;
            TempData["Provider"] = model.Provider;
            TempData["Method"] = model.MethodCode ?? "";
            TempData["HeldSeats"] = model.HeldSeats;
            return RedirectToAction("Index", new { id });
        }

        HttpContext.Session.Remove(SessionBookingKey);
        if (model.HeldSeats != null && booking != null)
        {
            TempData["InitCombos"] = string.Join(",", booking.Combos.Select(c => c.ComboId + ":" + c.Quantity));
            TempData["InitPromo"] = promotionCode;
        }
        return RedirectToAction("Index", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Abort(Guid id)
    {
        var bookingId = SessionBookingId();
        if (bookingId == null)
        {
            TempData["Notice"] = "Đã hủy đặt vé.";
            return RedirectToAction("Index", "Home");
        }

        var result = await _bookings.CancelPendingAsync(bookingId.Value, User.UserId());
        if (!result.Succeeded)
        {
            TempData["AbortError"] = result.Error;
            return RedirectToAction("Index", new { id });
        }

        HttpContext.Session.Remove(SessionBookingKey);
        TempData["Notice"] = "Đã hủy đặt vé. Ghế bạn giữ đã được trả lại.";
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Expired(Guid id)
    {
        HttpContext.Session.Remove(SessionBookingKey);
        TempData["Notice"] = "Đã hết thời gian giữ ghế nên đơn hàng bị hủy. Ghế đã được trả lại, bạn có thể đặt lại.";
        return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> Result(Guid id, string? vnp_ResponseCode, string? payment)
    {
        var model = new BookingResultViewModel();
        model.Booking = await _bookings.GetForUserAsync(id, User.UserId());
        model.ErrorMessage = model.Booking == null ? "Không tìm thấy đơn hàng." : null;
        model.VnpCode = vnp_ResponseCode;
        model.MockFailed = payment == "failed";
        model.CancelError = TempData["CancelError"] as string;
        return View(model);
    }

    public async Task<IActionResult> Status(Guid id)
    {
        var booking = await _bookings.GetForUserAsync(id, User.UserId());
        if (booking == null)
            return NotFound(new { message = "Không tìm thấy đơn hàng." });
        return Json(booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, string? vnp_ResponseCode, string? payment)
    {
        var result = await _bookings.CancelPendingAsync(id, User.UserId());
        if (!result.Succeeded)
        {
            TempData["CancelError"] = result.Error;
            return RedirectToAction("Result", new { id, vnp_ResponseCode, payment });
        }

        TempData["Notice"] = "Đã hủy đặt vé. Ghế bạn giữ đã được trả lại.";
        return RedirectToAction("Index", "Home");
    }

    private Task<List<ComboDto>> LoadCombosAsync()
    {
        return _context.Combos
            .Where(c => c.IsActive)
            .OrderBy(c => c.Price)
            .Select(c => new ComboDto(c.Id, c.Name, c.Description, c.Price, c.ImageUrl))
            .ToListAsync();
    }

    private Guid? SessionBookingId()
    {
        var value = HttpContext.Session.GetString(SessionBookingKey);
        return Guid.TryParse(value, out var bookingId) ? bookingId : null;
    }

    private static List<Guid> ParseIds(string text)
    {
        var list = new List<Guid>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Guid.TryParse(part, out var value) && !list.Contains(value))
                list.Add(value);
        }
        return list;
    }

    private static string FormatCombos(List<ComboQuantityViewModel> combos)
    {
        return string.Join(",", combos.Where(c => c.Quantity > 0).Select(c => c.ComboId + ":" + c.Quantity));
    }

    private static Dictionary<Guid, int> ParseCombos(string text)
    {
        var result = new Dictionary<Guid, int>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split(':');
            if (pieces.Length == 2 && Guid.TryParse(pieces[0], out var comboId) && int.TryParse(pieces[1], out var quantity))
                result[comboId] = quantity;
        }
        return result;
    }
}
