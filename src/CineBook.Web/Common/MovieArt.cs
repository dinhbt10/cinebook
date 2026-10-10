namespace CineBook.Web.Common;

public class MovieArt
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Accent { get; set; } = "";
    public string Glow { get; set; } = "";
    public int Angle { get; set; }
    public string Initial { get; set; } = "";
    public int Hue { get; set; }
    public int Sat { get; set; }

    private static readonly (int H, int S)[] Palette =
    {
        (348, 46), (212, 44), (272, 38), (162, 34), (32, 52), (232, 40), (316, 36),
    };

    private static long Hash(string seed)
    {
        int h = unchecked((int)2166136261);
        foreach (char c in seed)
        {
            h ^= c;
            h = unchecked(h * 16777619);
        }
        return Math.Abs((long)h);
    }

    public static MovieArt For(string seed)
    {
        var n = Hash(string.IsNullOrEmpty(seed) ? "cinebook" : seed);
        var b = Palette[n % Palette.Length];
        var shift = 18 + (int)(n % 26);
        var trimmed = (seed ?? "").Trim();

        return new MovieArt
        {
            From = $"hsl({b.H} {b.S}% 22%)",
            To = $"hsl({(b.H + shift) % 360} {Math.Max(18, b.S - 14)}% 9%)",
            Accent = $"hsl({(b.H + 180) % 360} 62% 62%)",
            Glow = $"hsl({b.H} {b.S + 14}% 52%)",
            Angle = 120 + (int)(n % 90),
            Initial = trimmed.Length > 0 ? trimmed.Substring(0, 1).ToUpper() : "?",
            Hue = b.H,
            Sat = b.S,
        };
    }

    public static string Background(string seed)
    {
        var a = For(seed);
        var glow = $"hsl({a.Hue} {a.Sat + 14}% 52% / 0.22)";
        var accent = $"hsl({(a.Hue + 180) % 360} 62% 62% / 0.15)";
        return $"radial-gradient(90% 120% at 12% 8%, {glow} 0%, transparent 58%), " +
               $"radial-gradient(70% 90% at 88% 22%, {accent} 0%, transparent 60%), " +
               $"linear-gradient({a.Angle}deg, {a.From} 0%, {a.To} 100%)";
    }

    public static bool IsPlaceholder(string? url) =>
        string.IsNullOrEmpty(url) || url.Contains("placehold.co");
}
