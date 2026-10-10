using CineBook.Web.Common;
using Xunit;

namespace CineBook.Web.Tests;

public class MovieArtTests
{
    [Fact]
    public void Same_title_always_gives_same_colors()
    {
        var a = MovieArt.For("Mai");
        var b = MovieArt.For("Mai");
        Assert.Equal(a.From, b.From);
        Assert.Equal(a.Angle, b.Angle);
        Assert.Equal("M", a.Initial);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("https://placehold.co/300x450", true)]
    [InlineData("/images/posters/a.png", false)]
    public void Placeholder_urls_are_detected(string? url, bool expected)
    {
        Assert.Equal(expected, MovieArt.IsPlaceholder(url));
    }
}
