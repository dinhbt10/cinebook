using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CineBook.Web.Common;

public static class HtmlExtensions
{
    public static IHtmlContent Icon(this IHtmlHelper html, string name, int size = 24,
        string? cssClass = null, bool filled = false)
    {
        if (!Icons.All.TryGetValue(name, out var icon))
            return HtmlString.Empty;

        var css = string.IsNullOrEmpty(cssClass) ? icon.Class : icon.Class + " " + cssClass;
        var fill = filled ? "currentColor" : "none";

        return new HtmlString(
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size}\" height=\"{size}\" viewBox=\"0 0 24 24\" " +
            $"fill=\"{fill}\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" " +
            $"stroke-linejoin=\"round\" class=\"{css}\" aria-hidden=\"true\">{icon.Body}</svg>");
    }

    public static string Css(params object?[] parts)
    {
        var list = new List<string>();
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] is string s && !string.IsNullOrWhiteSpace(s))
            {
                if (i + 1 < parts.Length && parts[i + 1] is bool flag)
                {
                    if (flag) list.Add(s);
                    i++;
                }
                else
                {
                    list.Add(s);
                }
            }
            else if (parts[i] is bool)
            {
                continue;
            }
        }
        return string.Join(" ", list);
    }
}
