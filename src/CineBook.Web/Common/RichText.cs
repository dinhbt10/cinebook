using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace CineBook.Web.Common;

public static class RichText
{
    private static readonly HashSet<string> AllowedTags = new()
    {
        "p", "br", "strong", "b", "em", "i", "u", "s", "ul", "ol", "li", "a", "h2", "h3", "h4", "blockquote",
    };

    private static readonly Regex Tag = new(@"<(/?)([a-zA-Z0-9]+)([^>]*)>", RegexOptions.Compiled);
    private static readonly Regex Href = new(@"href\s*=\s*[""']([^""']*)[""']", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Dangerous = new(@"<(script|style|iframe|object|embed)[^>]*>.*?</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    public static bool IsHtml(string? text) =>
        !string.IsNullOrEmpty(text) && Regex.IsMatch(text, @"<\s*/?\s*[a-zA-Z][^>]*>");

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var cleaned = Dangerous.Replace(html, "");

        return Tag.Replace(cleaned, match =>
        {
            var closing = match.Groups[1].Value;
            var name = match.Groups[2].Value.ToLowerInvariant();
            if (!AllowedTags.Contains(name)) return "";

            if (closing.Length > 0) return $"</{name}>";
            if (name != "a") return name == "br" ? "<br>" : $"<{name}>";

            var href = Href.Match(match.Groups[3].Value);
            var url = href.Success ? WebUtility.HtmlDecode(href.Groups[1].Value) : "";
            if (!url.StartsWith("http://") && !url.StartsWith("https://")) return "<a>";
            return $"<a href=\"{WebUtility.HtmlEncode(url)}\" target=\"_blank\" rel=\"noreferrer\">";
        });
    }

    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        if (!IsHtml(html)) return html;
        var text = Regex.Replace(html, @"<(br|/p|/li|/h[2-4])\s*/?>", " ", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "<[^>]+>", "");
        return Regex.Replace(WebUtility.HtmlDecode(text), @"\s+", " ").Trim();
    }

    public static IHtmlContent Render(string? text) =>
        IsHtml(text) ? new HtmlString(Sanitize(text)) : new HtmlString(WebUtility.HtmlEncode(text ?? ""));
}
