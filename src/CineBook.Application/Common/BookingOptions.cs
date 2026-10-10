namespace CineBook.Application.Common;

public class BookingOptions
{
    public const string SectionName = "Booking";

    public int HoldMinutes { get; set; } = 8;

    public int MaxSeatsPerBooking { get; set; } = 8;

    public int MinutesBeforeStartToBlock { get; set; } = 15;

    public int RoomCleanupMinutes { get; set; } = 15;

    public int RefundFullBeforeHours { get; set; } = 24;
    public int RefundPartialBeforeHours { get; set; } = 2;
    public decimal RefundPartialRate { get; set; } = 0.8m;

    public int CheckInOpenBeforeMinutes { get; set; } = 60;
    public int CheckInCloseAfterMinutes { get; set; } = 30;

    public int ExpireJobIntervalMinutes { get; set; } = 30;
}
