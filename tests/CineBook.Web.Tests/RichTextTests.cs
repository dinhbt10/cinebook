using CineBook.Web.Common;
using Xunit;

namespace CineBook.Web.Tests;

public class RichTextTests
{
    [Fact]
    public void Plain_text_is_encoded()
    {
        Assert.Equal("a &lt; b", RichText.Render("a < b").ToString());
    }

    [Fact]
    public void Script_tags_are_removed()
    {
        var html = RichText.Sanitize("<p>Hay</p><script>alert(1)</script>");
        Assert.Equal("<p>Hay</p>", html);
    }

    [Fact]
    public void Event_handler_attributes_are_dropped()
    {
        var html = RichText.Sanitize("<p onclick=\"alert(1)\">Phim <strong>hay</strong></p>");
        Assert.Equal("<p>Phim <strong>hay</strong></p>", html);
    }

    [Fact]
    public void Javascript_links_lose_their_href()
    {
        Assert.Equal("<a>x</a>", RichText.Sanitize("<a href=\"javascript:alert(1)\">x</a>"));
        Assert.Equal("<a href=\"https://cinebook.vn\" target=\"_blank\" rel=\"noreferrer\">x</a>",
            RichText.Sanitize("<a href=\"https://cinebook.vn\">x</a>"));
    }

    [Fact]
    public void Plain_text_summary_strips_tags()
    {
        Assert.Equal("Phim hay lắm", RichText.ToPlainText("<p>Phim <em>hay</em></p><p>lắm</p>"));
    }
}
