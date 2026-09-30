using CineBook.Domain.Enums;

namespace CineBook.Application.Dtos;

// ---------- Xác thực ----------
public record RegisterRequest(string Email, string Password, string FullName, string? Phone);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserDto User);
public record UserDto(Guid Id, string Email, string FullName, string? Phone, string Role, Guid? CinemaId);

// ---------- Phim ----------
public record MovieListItemDto(Guid Id, string Title, int DurationMinutes, string AgeRating,
    string? PosterUrl, string? BackdropUrl, string? TrailerUrl,
    DateOnly ReleaseDate, string Status, IEnumerable<string> Genres);

public record MovieDetailDto(Guid Id, string Title, string? OriginalTitle, string? Description,
    int DurationMinutes, string AgeRating, string? Director, string? Cast, string? Language,
    string? PosterUrl, string? BackdropUrl, string? TrailerUrl, DateOnly ReleaseDate,
    string Status, IEnumerable<string> Genres,
    string? Country, DateOnly? EndDate, IEnumerable<Guid> GenreIds);

public record SaveMovieRequest(string Title, string? OriginalTitle, string? Description,
    int DurationMinutes, DateOnly ReleaseDate, DateOnly? EndDate, AgeRating AgeRating,
    string? Director, string? Cast, string? Language, string? Country,
    string? PosterUrl, string? BackdropUrl, string? TrailerUrl, MovieStatus Status,
    IEnumerable<Guid>? GenreIds);

// ---------- Rạp, phòng ----------
public record CinemaDto(Guid Id, string Name, string City, string? District, string Address, string? Hotline);
public record RoomDto(Guid Id, string Name, string RoomType, int SeatCount);
public record SaveCinemaRequest(string Name, string City, string? District, string Address, string? Hotline);

/// <summary>Tạo phòng bằng số hàng × số cột, sinh ghế tự động (bản đơn giản ở §9.2).</summary>
public record CreateRoomRequest(Guid CinemaId, string Name, RoomType RoomType,
    int RowCount, int SeatsPerRow, int VipFromRow, int VipToRow);

// ---------- Suất chiếu ----------
public record ShowtimeListItemDto(Guid Id, Guid MovieId, string MovieTitle, Guid RoomId, string RoomName,
    string CinemaName, string RoomType, DateTime StartsAtUtc, DateTime EndsAtUtc,
    string SubtitleMode, decimal FromPrice, int AvailableSeats, int TotalSeats, bool IsOnSale,
    bool IsCancelled = false);

/// <summary>Giá vé theo từng loại ghế của một suất. Ghế ưu tiên tính như ghế thường.</summary>
public record SeatTypePrices(decimal Standard, decimal Vip, decimal Couple);

/// <summary>
/// SeatPrices để trống thì giá từng ghế tính theo bảng giá (BR-18) từ BasePrice;
/// có thì dùng đúng giá quản trị đã chốt cho từng loại ghế.
/// </summary>
public record CreateShowtimeRequest(Guid MovieId, Guid RoomId, DateTime StartsAtUtc,
    decimal BasePrice, SubtitleMode SubtitleMode, bool OpenForSale, SeatTypePrices? SeatPrices = null);

// ---------- Sơ đồ ghế ----------
public record SeatMapSeatDto(Guid SeatId, string RowLabel, int Number, string Label,
    string SeatType, string Status, decimal Price, bool HeldByMe);

public record SeatMapDto(Guid ShowtimeId, string MovieTitle, string CinemaName, string RoomName,
    DateTime StartsAtUtc, int MaxSeatsPerBooking, IEnumerable<SeatMapSeatDto> Seats);

// ---------- Đặt vé ----------
public record ComboSelection(Guid ComboId, int Quantity);
public record HoldSeatsRequest(Guid ShowtimeId, IReadOnlyList<Guid> SeatIds,
    IReadOnlyList<ComboSelection>? Combos, string? PromotionCode);

public record BookingSeatDto(Guid SeatId, string Label, string SeatType, decimal Price);
public record BookingComboDto(Guid ComboId, string Name, int Quantity, decimal UnitPrice);

public record BookingDto(Guid Id, string Code, string Status, Guid ShowtimeId, string MovieTitle,
    string CinemaName, string RoomName, DateTime StartsAtUtc,
    IEnumerable<BookingSeatDto> Seats, IEnumerable<BookingComboDto> Combos,
    decimal SeatsSubtotal, decimal ConcessionSubtotal, decimal DiscountAmount, decimal Total,
    DateTime HoldExpiresAtUtc, int SecondsLeft, DateTime? PaidAtUtc, string? CancelReason = null);

// ---------- Thanh toán ----------
public record PaymentMethodDto(string Code, string DisplayName);
public record PaymentProviderDto(string Provider, string DisplayName, IReadOnlyList<PaymentMethodDto> Methods);

/// <summary>MethodCode để trống thì cổng tự cho khách chọn trên trang của mình.</summary>
public record CreatePaymentRequest(string Provider, string ReturnUrl, string? MethodCode = null);
public record CreatePaymentResponse(Guid PaymentId, string Provider, string ProviderTxnId, string PaymentUrl);

// ---------- Vé ----------
public record TicketDto(Guid Id, string Code, string QrPayload, string SeatLabel, string Status,
    string MovieTitle, string CinemaName, string RoomName, DateTime StartsAtUtc, DateTime? CheckedInAtUtc);

// ---------- Soát vé ----------
public record CheckInRequest(string QrPayload);
public record CheckInResponse(bool Allowed, string Verdict, string Message,
    string? TicketCode, string? SeatLabel, string? MovieTitle, string? RoomName, DateTime? StartsAtUtc);

// ---------- Bắp nước, khuyến mãi ----------
public record ComboDto(Guid Id, string Name, string? Description, decimal Price, string? ImageUrl);
public record PromotionDto(Guid Id, string Code, string Name, string DiscountType,
    decimal Value, decimal? MaxDiscount, decimal MinOrderAmount, DateTime EndsAtUtc);

/// <summary>
/// Voucher kèm kết quả thẩm định cho MỘT đơn hàng cụ thể, để giao diện hiện
/// sẵn số tiền được giảm và lý do nếu chưa dùng được — khách không phải đoán.
/// </summary>
public record VoucherDto(Guid Id, string Code, string Name, string DiscountType,
    decimal Value, decimal? MaxDiscount, decimal MinOrderAmount, DateTime EndsAtUtc,
    bool Saved, bool Usable, string? Reason, decimal DiscountAmount);

/// <summary>
/// Voucher cho trang quản trị: đủ mọi trường, kể cả voucher đã tắt hay hết hạn.
/// Locked = đã có đơn dùng, nên không đổi được mã, loại giảm, giá trị và không xoá hẳn được.
/// </summary>
public record AdminPromotionDto(Guid Id, string Code, string Name, string DiscountType,
    decimal Value, decimal? MaxDiscount, decimal MinOrderAmount, DateTime StartsAtUtc, DateTime EndsAtUtc,
    int? UsageLimit, int UsedCount, int PerUserLimit, bool IsActive, bool Locked);

public record SavePromotionRequest(string Code, string Name, DiscountType DiscountType, decimal Value,
    decimal? MaxDiscount, decimal MinOrderAmount, DateTime StartsAtUtc, DateTime EndsAtUtc,
    int? UsageLimit, int PerUserLimit, bool IsActive);

// ---------- Báo cáo ----------
public record RevenuePointDto(DateOnly Date, decimal TicketRevenue, decimal ConcessionRevenue, int BookingCount);
public record MovieRevenueDto(Guid MovieId, string Title, decimal Revenue, int TicketCount, double OccupancyRate);
public record DashboardDto(decimal RevenueToday, decimal RevenueThisMonth, int TicketsToday,
    int BookingsToday, double OccupancyToday, int ShowtimesToday);
