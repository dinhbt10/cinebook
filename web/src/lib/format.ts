/** Giờ Việt Nam so với UTC. Backend luôn trả về UTC. */
const VN_OFFSET_MS = 7 * 60 * 60 * 1000

export const toVN = (utc: string | Date) =>
  new Date(new Date(utc).getTime() + VN_OFFSET_MS)

const pad = (n: number) => String(n).padStart(2, '0')

export const money = (v: number) => `${Math.round(v).toLocaleString('vi-VN')}đ`

export const time = (utc: string) => {
  const d = toVN(utc)
  return `${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`
}

export const dayMonth = (utc: string) => {
  const d = toVN(utc)
  return `${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}`
}

export const fullDateTime = (utc: string) => {
  const d = toVN(utc)
  return `${time(utc)} · ${pad(d.getUTCDate())}/${pad(d.getUTCMonth() + 1)}/${d.getUTCFullYear()}`
}

const WEEKDAYS = ['Chủ nhật', 'Thứ hai', 'Thứ ba', 'Thứ tư', 'Thứ năm', 'Thứ sáu', 'Thứ bảy']
export const weekday = (utc: string | Date) => WEEKDAYS[toVN(utc).getUTCDay()]
export const weekdayShort = (utc: string | Date) => {
  const d = toVN(utc).getUTCDay()
  return d === 0 ? 'CN' : `T${d + 1}`
}

/** Ngày dạng yyyy-MM-dd theo giờ Việt Nam, dùng cho tham số API. */
export const isoDateVN = (d: Date) => {
  const v = new Date(d.getTime() + VN_OFFSET_MS)
  return `${v.getUTCFullYear()}-${pad(v.getUTCMonth() + 1)}-${pad(v.getUTCDate())}`
}

/** Giá trị cho ô datetime-local, hiện theo giờ Việt Nam dù máy đặt múi giờ nào. */
export const vnInputFromUtc = (utc: string | Date) => {
  const d = toVN(utc)
  return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}` +
    `T${pad(d.getUTCHours())}:${pad(d.getUTCMinutes())}`
}

/**
 * Giá trị ô datetime-local (giờ Việt Nam) sang ISO UTC cho backend. Tự tách số chứ
 * không dùng new Date(value): new Date hiểu chuỗi theo múi giờ của máy, nên máy đặt
 * giờ Việt Nam mà trừ thêm 7 tiếng sẽ lệch đúng 7 tiếng.
 */
export const utcFromVnInput = (local: string) => {
  const [date, clock = '00:00'] = local.split('T')
  const [y, m, d] = date.split('-').map(Number)
  const [hh, mm] = clock.split(':').map(Number)
  return new Date(Date.UTC(y, m - 1, d, hh, mm) - VN_OFFSET_MS).toISOString()
}

export const countdown = (seconds: number) =>
  `${Math.floor(Math.max(0, seconds) / 60)}:${pad(Math.max(0, seconds) % 60)}`

export const AGE_RATING_LABEL: Record<string, string> = {
  P: 'P — mọi lứa tuổi',
  K: 'K — dưới 13 tuổi cần người lớn đi kèm',
  T13: 'T13 — từ 13 tuổi',
  T16: 'T16 — từ 16 tuổi',
  T18: 'T18 — từ 18 tuổi',
}

export const ROOM_TYPE_LABEL: Record<string, string> = {
  TwoD: '2D', ThreeD: '3D', Imax: 'IMAX',
}

export const SUBTITLE_LABEL: Record<string, string> = {
  Subtitled: 'Phụ đề', Dubbed: 'Lồng tiếng', Original: 'Nguyên bản',
}

export const SEAT_TYPE_LABEL: Record<string, string> = {
  Standard: 'Thường', Vip: 'VIP', Couple: 'Ghế đôi', Accessible: 'Ưu tiên',
}

/** Diễn giải mã lỗi VNPay trả về trên đường khách quay lại. */
export const VNPAY_MESSAGE: Record<string, string> = {
  '00': 'Giao dịch thành công.',
  '07': 'Giao dịch bị nghi ngờ gian lận.',
  '09': 'Thẻ chưa đăng ký dịch vụ InternetBanking.',
  '10': 'Xác thực thông tin thẻ sai quá 3 lần.',
  '11': 'Đã hết hạn chờ thanh toán.',
  '12': 'Thẻ hoặc tài khoản đang bị khóa.',
  '13': 'Nhập sai mật khẩu xác thực OTP.',
  '24': 'Bạn đã hủy giao dịch.',
  '51': 'Tài khoản không đủ số dư.',
  '65': 'Tài khoản đã vượt hạn mức giao dịch trong ngày.',
  '75': 'Ngân hàng thanh toán đang bảo trì.',
  '79': 'Nhập sai mật khẩu thanh toán quá số lần quy định.',
}

export const BOOKING_STATUS: Record<string, { label: string; tone: string }> = {
  Pending:   { label: 'Chờ thanh toán', tone: 'brand' },
  Paid:      { label: 'Đã thanh toán',  tone: 'mint' },
  Expired:   { label: 'Hết hạn giữ ghế', tone: 'faint' },
  Cancelled: { label: 'Đã hủy',          tone: 'velvet' },
  Refunded:  { label: 'Đã hoàn tiền',    tone: 'velvet' },
}
