using CineBook.Web.Common;
using Xunit;

namespace CineBook.Web.Tests;

public class FormatTests
{
    [Theory]
    [InlineData(0, "0đ")]
    [InlineData(75000, "75.000đ")]
    [InlineData(1234567.6, "1.234.568đ")]
    public void Money_uses_vietnamese_grouping(decimal value, string expected)
    {
        Assert.Equal(expected, Fmt.Money(value));
    }

    [Fact]
    public void Time_is_converted_from_utc_to_vietnam()
    {
        var utc = new DateTime(2026, 10, 10, 13, 5, 0, DateTimeKind.Utc);
        Assert.Equal("20:05", Fmt.Time(utc));
        Assert.Equal("20:05 · 10/10/2026", Fmt.FullDateTime(utc));
    }

    [Fact]
    public void Late_evening_utc_rolls_over_to_next_vietnam_day()
    {
        var utc = new DateTime(2026, 10, 10, 18, 30, 0, DateTimeKind.Utc);
        Assert.Equal("11/10", Fmt.DayMonth(utc));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(480, "8:00")]
    [InlineData(65, "1:05")]
    [InlineData(-3, "0:00")]
    public void Countdown_formats_minutes_and_seconds(int seconds, string expected)
    {
        Assert.Equal(expected, Fmt.Countdown(seconds));
    }

    [Fact]
    public void Weekday_short_uses_vietnamese_names()
    {
        Assert.Equal("CN", Fmt.WeekdayShort(new DateOnly(2026, 10, 11)));
        Assert.Equal("T2", Fmt.WeekdayShort(new DateOnly(2026, 10, 12)));
        Assert.Equal("T7", Fmt.WeekdayShort(new DateOnly(2026, 10, 10)));
    }
}
