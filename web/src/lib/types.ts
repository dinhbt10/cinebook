export type Role = 'Customer' | 'Staff' | 'CinemaManager' | 'Admin'

export interface User {
  id: string; email: string; fullName: string
  phone: string | null; role: Role; cinemaId: string | null
}
export interface AuthResponse { accessToken: string; expiresAtUtc: string; user: User }

export interface MovieListItem {
  id: string; title: string; durationMinutes: number; ageRating: string
  posterUrl: string | null; backdropUrl: string | null; trailerUrl: string | null
  releaseDate: string; status: string; genres: string[]
}
export interface MovieDetail extends MovieListItem {
  originalTitle: string | null; description: string | null
  director: string | null; cast: string | null; language: string | null
}

export interface Cinema {
  id: string; name: string; city: string
  district: string | null; address: string; hotline: string | null
}
export interface Room { id: string; name: string; roomType: string; seatCount: number }

export interface Showtime {
  id: string; movieId: string; movieTitle: string; roomId: string; roomName: string
  cinemaName: string; roomType: string; startsAtUtc: string; endsAtUtc: string
  subtitleMode: string; fromPrice: number
  availableSeats: number; totalSeats: number; isOnSale: boolean
}

/** Giá vé theo từng loại ghế của một suất (/api/showtimes/price-preview). */
export interface SeatTypePrices { standard: number; vip: number; couple: number }

export type SeatStatus = 'Available' | 'Held' | 'Sold' | 'Maintenance'
export interface SeatMapSeat {
  seatId: string; rowLabel: string; number: number; label: string
  seatType: 'Standard' | 'Vip' | 'Couple' | 'Accessible'
  status: SeatStatus; price: number; heldByMe: boolean
}
export interface SeatMap {
  showtimeId: string; movieTitle: string; cinemaName: string; roomName: string
  startsAtUtc: string; maxSeatsPerBooking: number; seats: SeatMapSeat[]
}

export interface Combo {
  id: string; name: string; description: string | null; price: number; imageUrl: string | null
}
export interface Promotion {
  id: string; code: string; name: string; discountType: string
  value: number; maxDiscount: number | null; minOrderAmount: number; endsAtUtc: string
}

/** Voucher kèm kết quả thẩm định cho một đơn cụ thể (/api/promotions/for-order). */
export interface Voucher extends Promotion {
  /** Đã lưu trong kho voucher của khách chưa. */
  saved: boolean
  /** Dùng được cho đơn hiện tại không. */
  usable: boolean
  /** Lý do chưa dùng được, ví dụ còn thiếu bao nhiêu tiền. */
  reason: string | null
  /** Số tiền giảm thật cho đơn này. */
  discountAmount: number
}

/** Voucher ở trang quản trị (/api/promotions/all), kể cả đã tắt hay hết hạn. */
export interface AdminPromotion {
  id: string; code: string; name: string; discountType: 'Percentage' | 'FixedAmount'
  value: number; maxDiscount: number | null; minOrderAmount: number
  startsAtUtc: string; endsAtUtc: string
  usageLimit: number | null; usedCount: number; perUserLimit: number; isActive: boolean
  /** Đã có đơn dùng: không đổi được mã, loại giảm, giá trị; bấm xoá thì chỉ tắt. */
  locked: boolean
}

/** Một cách trả tiền trong cùng một cổng, ví dụ quét QR hay thẻ ATM. */
export interface PaymentMethodOption { code: string; displayName: string }

/** Cổng thanh toán máy chủ đang bật, lấy từ /api/payments/providers. */
export interface PaymentProvider {
  provider: string
  displayName: string
  methods: PaymentMethodOption[]
}

export type BookingStatus = 'Pending' | 'Paid' | 'Expired' | 'Cancelled' | 'Refunded'
export interface Booking {
  id: string; code: string; status: BookingStatus; showtimeId: string
  movieTitle: string; cinemaName: string; roomName: string; startsAtUtc: string
  seats: { seatId: string; label: string; seatType: string; price: number }[]
  combos: { comboId: string; name: string; quantity: number; unitPrice: number }[]
  seatsSubtotal: number; concessionSubtotal: number; discountAmount: number; total: number
  holdExpiresAtUtc: string; secondsLeft: number; paidAtUtc: string | null
}

export interface Ticket {
  id: string; code: string; qrPayload: string; seatLabel: string; status: string
  movieTitle: string; cinemaName: string; roomName: string
  startsAtUtc: string; checkedInAtUtc: string | null
}

export interface CheckInResult {
  allowed: boolean; verdict: string; message: string
  ticketCode: string | null; seatLabel: string | null
  movieTitle: string | null; roomName: string | null; startsAtUtc: string | null
}

export interface Dashboard {
  revenueToday: number; revenueThisMonth: number; ticketsToday: number
  bookingsToday: number; occupancyToday: number; showtimesToday: number
}
export interface RevenuePoint {
  date: string; ticketRevenue: number; concessionRevenue: number; bookingCount: number
}
export interface MovieRevenue {
  movieId: string; title: string; revenue: number; ticketCount: number; occupancyRate: number
}
