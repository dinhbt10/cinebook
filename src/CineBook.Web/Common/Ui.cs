namespace CineBook.Web.Common;

public static class Ui
{
    private static readonly Dictionary<string, string> Variants = new()
    {
        ["primary"] = "bg-brand-500 text-on-brand hover:bg-brand-400 font-semibold shadow-[0_1px_0_rgba(255,255,255,.25)_inset]",
        ["secondary"] = "bg-ink-800 text-cream border border-ink-600 hover:border-ink-500 hover:bg-ink-700",
        ["ghost"] = "text-muted hover:text-cream hover:bg-ink-800",
        ["danger"] = "bg-velvet-500 text-white hover:bg-velvet-400 font-semibold",
    };

    private static readonly Dictionary<string, string> Sizes = new()
    {
        ["sm"] = "h-9 px-3.5 text-[13px] rounded-lg gap-1.5",
        ["md"] = "h-11 px-5 text-sm rounded-xl gap-2",
        ["lg"] = "h-13 px-7 text-[15px] rounded-xl gap-2",
    };

    private static readonly Dictionary<string, string> Tones = new()
    {
        ["brand"] = "bg-brand-950 text-brand-400 border-brand-600/40",
        ["mint"] = "bg-mint-950 text-mint-400 border-mint-500/35",
        ["velvet"] = "bg-velvet-950 text-velvet-400 border-velvet-500/35",
        ["faint"] = "bg-ink-800 text-faint border-ink-600",
        ["outline"] = "bg-transparent text-muted border-ink-600",
    };

    public static string Button(string variant = "primary", string size = "md", string? extra = null) =>
        "inline-flex items-center justify-center transition-all duration-150 " +
        "disabled:opacity-45 disabled:cursor-not-allowed active:scale-[.985] " +
        Variants[variant] + " " + Sizes[size] + (extra == null ? "" : " " + extra);

    public static string Badge(string tone = "outline", string? extra = null) =>
        "inline-flex items-center gap-1 rounded-md border px-2 py-[3px] text-[11px] font-medium leading-none " +
        (Tones.TryGetValue(tone, out var t) ? t : Tones["outline"]) + (extra == null ? "" : " " + extra);

    public const string Field =
        "w-full rounded-xl border border-ink-600 bg-ink-900 px-3.5 py-2.5 text-sm text-cream " +
        "placeholder:text-faint transition-colors focus:border-brand-600 focus:outline-none " +
        "disabled:opacity-50";

    public static string Input(string? extra = null) => Field + (extra == null ? "" : " " + extra);

    public static string Textarea(string? extra = null) =>
        Field + " min-h-24 resize-y" + (extra == null ? "" : " " + extra);

    public static string Select(string? extra = null) =>
        Field + " appearance-none pr-9 cursor-pointer" + (extra == null ? "" : " " + extra);

    public const string Label = "text-[13px] font-medium text-muted";

    public const string Hint = "text-[12px] text-faint";

    public const string FieldError = "text-[12px] text-velvet-400";

    public const string ErrorNote =
        "rounded-xl border border-velvet-500/35 bg-velvet-950 px-3.5 py-3 text-[13px] text-velvet-400";

    public static readonly Dictionary<string, string> RatingTone = new()
    {
        ["P"] = "mint", ["K"] = "mint", ["T13"] = "brand", ["T16"] = "brand", ["T18"] = "velvet",
    };
}
