namespace CineBook.Domain.Enums;

/// <summary>Bốn vai trò của hệ thống (BR-19).</summary>
public enum UserRole { Customer = 0, Staff = 1, CinemaManager = 2, Admin = 3 }

/// <summary>Nhãn phân loại độ tuổi phim theo quy định hiện hành ở Việt Nam.</summary>
public enum AgeRating { P = 0, K = 1, T13 = 2, T16 = 3, T18 = 4 }

public enum MovieStatus { Draft = 0, ComingSoon = 1, NowShowing = 2, Ended = 3 }

public enum RoomType { TwoD = 0, ThreeD = 1, Imax = 2 }

public enum SeatType { Standard = 0, Vip = 1, Couple = 2, Accessible = 3 }

/// <summary>Trạng thái một ghế trong MỘT suất chiếu — xem §04 tài liệu flow.</summary>
public enum SeatStatus { Available = 0, Held = 1, Sold = 2, Maintenance = 3 }

/// <summary>Cách trình chiếu ngôn ngữ: phụ đề hay lồng tiếng.</summary>
public enum SubtitleMode { Subtitled = 0, Dubbed = 1, Original = 2 }

/// <summary>Vòng đời đơn đặt vé — xem §03 tài liệu flow.</summary>
public enum BookingStatus { Pending = 0, Paid = 1, Expired = 2, Cancelled = 3, Refunded = 4 }

public enum TicketStatus { Valid = 0, Used = 1, Cancelled = 2 }

public enum PaymentStatus { Pending = 0, Succeeded = 1, Failed = 2, Refunded = 3 }

public enum DiscountType { Percentage = 0, FixedAmount = 1 }
