using CineBook.Application.Services;
using CineBook.Web.Common;
using CineBook.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CineBook.Web.Controllers;

[Authorize]
public class TicketsController : Controller
{
    private readonly BookingService _bookings;
    private readonly TicketService _tickets;

    public TicketsController(BookingService bookings, TicketService tickets)
    {
        _bookings = bookings;
        _tickets = tickets;
    }

    public async Task<IActionResult> Index()
    {
        var model = new TicketsPageViewModel();
        model.Bookings = (await _bookings.GetMyBookingsAsync(User.UserId())).ToList();
        model.CancelError = TempData["CancelError"] as string;
        if (Guid.TryParse(TempData["CancelBookingId"] as string, out var cancelId))
            model.CancelBookingId = cancelId;
        return View(model);
    }

    public async Task<IActionResult> Qr(Guid id)
    {
        var userId = User.UserId();
        var booking = await _bookings.GetForUserAsync(id, userId);
        if (booking == null)
            return NotFound();

        var model = new TicketQrViewModel();
        model.Booking = booking;
        foreach (var ticket in await _tickets.GetByBookingAsync(id, userId))
        {
            var item = new TicketQrItem();
            item.Ticket = ticket;
            item.Svg = QrSvg.Render(ticket.QrPayload, 116);
            model.Tickets.Add(item);
        }
        return PartialView("_TicketQr", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var result = await _bookings.CancelPendingAsync(id, User.UserId());
        if (!result.Succeeded)
        {
            TempData["CancelError"] = result.Error;
            TempData["CancelBookingId"] = id.ToString();
        }
        return RedirectToAction("Index");
    }
}
