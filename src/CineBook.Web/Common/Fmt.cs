using System.Globalization;

namespace CineBook.Web.Common;

public static class Fmt
{
    public static DateTime ToVn(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7);

    public static DateTime VnNow => DateTime.UtcNow.AddHours(7);

    public static DateOnly VnToday => DateOnly.FromDateTime(VnNow);

    public static string Money(decimal value)
    {
        var rounded = Math.Round(value, MidpointRounding.AwayFromZero);
        return rounded.ToString("#,0", CultureInfo.InvariantCulture).Replace(",", ".") + "đ";
    }

    public static string Time(DateTime utc) => ToVn(utc).ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string DayMonth(DateTime utc) => ToVn(utc).ToString("dd/MM", CultureInfo.InvariantCulture);

    public static string FullDateTime(DateTime utc) =>
        Time(utc) + " · " + ToVn(utc).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Percent(double rate, int decimals) =>
        (rate * 100).ToString(decimals == 0 ? "0" : "0." + new string('0', decimals), CultureInfo.InvariantCulture) + "%";

    public static string WeekdayShort(DateOnly date)
    {
        var d = (int)date.DayOfWeek;
        return d == 0 ? "CN" : "T" + (d + 1);
    }

    public static string Countdown(int seconds)
    {
        var s = Math.Max(0, seconds);
        return (s / 60) + ":" + (s % 60).ToString("00");
    }

    public static readonly Dictionary<string, string> AgeRatingLabel = new()
    {
        ["P"] = "P — mọi lứa tuổi",
        ["K"] = "K — dưới 13 tuổi cần người lớn đi kèm",
        ["T13"] = "T13 — từ 13 tuổi",
        ["T16"] = "T16 — từ 16 tuổi",
        ["T18"] = "T18 — từ 18 tuổi",
    };

    public static readonly Dictionary<string, string> RoomTypeLabel = new()
    {
        ["TwoD"] = "2D", ["ThreeD"] = "3D", ["Imax"] = "IMAX",
    };

    public static readonly Dictionary<string, string> SubtitleLabel = new()
    {
        ["Subtitled"] = "Phụ đề", ["Dubbed"] = "Lồng tiếng", ["Original"] = "Nguyên bản",
    };

    public static readonly Dictionary<string, string> SeatTypeLabel = new()
    {
        ["Standard"] = "Thường", ["Vip"] = "VIP", ["Couple"] = "Ghế đôi", ["Accessible"] = "Ưu tiên",
    };

    public static readonly Dictionary<string, string> VnPayMessage = new()
    {
        ["00"] = "Giao dịch thành công.",
        ["07"] = "Giao dịch bị nghi ngờ gian lận.",
        ["09"] = "Thẻ chưa đăng ký dịch vụ InternetBanking.",
        ["10"] = "Xác thực thông tin thẻ sai quá 3 lần.",
        ["11"] = "Đã hết hạn chờ thanh toán.",
        ["12"] = "Thẻ hoặc tài khoản đang bị khóa.",
        ["13"] = "Nhập sai mật khẩu xác thực OTP.",
        ["24"] = "Bạn đã hủy giao dịch.",
        ["51"] = "Tài khoản không đủ số dư.",
        ["65"] = "Tài khoản đã vượt hạn mức giao dịch trong ngày.",
        ["75"] = "Ngân hàng thanh toán đang bảo trì.",
        ["79"] = "Nhập sai mật khẩu thanh toán quá số lần quy định.",
    };

    public static readonly Dictionary<string, (string Label, string Tone)> BookingStatus = new()
    {
        ["Pending"] = ("Chờ thanh toán", "brand"),
        ["Paid"] = ("Đã thanh toán", "mint"),
        ["Expired"] = ("Hết hạn giữ ghế", "faint"),
        ["Cancelled"] = ("Đã hủy", "velvet"),
        ["Refunded"] = ("Đã hoàn tiền", "velvet"),
    };

    public static string Label(Dictionary<string, string> labels, string key) =>
        labels.TryGetValue(key, out var v) ? v : key;
}
