using Microsoft.AspNetCore.Html;

namespace CineBook.Web.Models;

public class PosterModel
{
    public string Title { get; set; } = "";
    public string? Url { get; set; }
    public bool Eager { get; set; }
    public string? CssClass { get; set; }
}

public class ConfirmDialogModel
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string ConfirmLabel { get; set; } = "";
    public string CancelLabel { get; set; } = "Quay lại";
    public string? Form { get; set; }
    public IHtmlContent? Body { get; set; }
    public string? Error { get; set; }
}
