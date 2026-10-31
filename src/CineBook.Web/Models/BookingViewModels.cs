using CineBook.Application.Dtos;

namespace CineBook.Web.Models;

public class BookingPageViewModel
{
    public SeatMapDto SeatMap { get; set; } = null!;
    public List<ComboDto> Combos { get; set; } = new List<ComboDto>();
    public List<PaymentProviderDto> Providers { get; set; } = new List<PaymentProviderDto>();
    public BookingDto? Booking { get; set; }
    public bool Resumed { get; set; }
    public string Step { get; set; } = "seats";
    public DateTime? SelectExpiresAtUtc { get; set; }
    public string? Error { get; set; }
    public string? AbortError { get; set; }
    public List<Guid> SelectedSeatIds { get; set; } = new List<Guid>();
    public Dictionary<Guid, int> ComboQuantities { get; set; } = new Dictionary<Guid, int>();
    public string? PromotionCode { get; set; }
    public string? Provider { get; set; }
    public string? Method { get; set; }

    public List<BookingSeatDto> ChosenSeats()
    {
        var list = new List<BookingSeatDto>();
        if (Booking == null) return list;
        foreach (var id in SelectedSeatIds)
        {
            var seat = Booking.Seats.FirstOrDefault(s => s.SeatId == id);
            if (seat != null) list.Add(seat);
        }
        return list;
    }
}

public class HoldSeatsViewModel
{
    public Guid ShowtimeId { get; set; }
    public List<Guid> SeatIds { get; set; } = new List<Guid>();
    public List<ComboQuantityViewModel> Combos { get; set; } = new List<ComboQuantityViewModel>();
    public string? PromotionCode { get; set; }
}

public class ComboQuantityViewModel
{
    public Guid ComboId { get; set; }
    public int Quantity { get; set; }
}

public class PayViewModel
{
    public string Provider { get; set; } = "";
    public string? MethodCode { get; set; }
    public string? HeldSeats { get; set; }
}

public class BookingResultViewModel
{
    public BookingDto? Booking { get; set; }
    public string? ErrorMessage { get; set; }
    public string? VnpCode { get; set; }
    public bool MockFailed { get; set; }
    public string? CancelError { get; set; }

    public bool Paid => Booking != null && Booking.Status == "Paid";

    public bool FailedAtGateway =>
        !Paid && ((!string.IsNullOrEmpty(VnpCode) && VnpCode != "00") || MockFailed);

    public bool Pending => Booking != null && Booking.Status == "Pending" && !FailedAtGateway;

    public bool CanRetry =>
        Booking != null && FailedAtGateway && Booking.Status == "Pending" && Booking.SecondsLeft > 0;
}

public class TicketsPageViewModel
{
    public List<BookingDto> Bookings { get; set; } = new List<BookingDto>();
    public Guid? CancelBookingId { get; set; }
    public string? CancelError { get; set; }
}

public class TicketQrViewModel
{
    public BookingDto Booking { get; set; } = null!;
    public List<TicketQrItem> Tickets { get; set; } = new List<TicketQrItem>();
}

public class TicketQrItem
{
    public TicketDto Ticket { get; set; } = null!;
    public string Svg { get; set; } = "";
}
