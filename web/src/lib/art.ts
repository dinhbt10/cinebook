/**
 * Sinh artwork cho phim ngay ở trình duyệt.
 *
 * Dữ liệu mẫu không có ảnh poster thật, mà một lưới toàn khối màu phẳng
 * thì trang nào cũng giống trang nào. Ở đây mỗi phim được gán một bảng màu
 * riêng suy ra từ chính tên phim, nên artwork luôn ổn định giữa các lần tải
 * và giữa các trang, không cần lưu gì thêm vào cơ sở dữ liệu.
 */

/** Băm chuỗi thành số nguyên ổn định (FNV-1a rút gọn). */
function hash(seed: string): number {
  let h = 2166136261
  for (let i = 0; i < seed.length; i++) {
    h ^= seed.charCodeAt(i)
    h = Math.imul(h, 16777619)
  }
  return Math.abs(h)
}

export interface MovieArt {
  from: string
  to: string
  accent: string
  glow: string
  angle: number
  /** Ký tự đầu của tên phim, dùng làm hoạ tiết lớn trên nền. */
  initial: string
  /** Sắc độ gốc, để dựng màu có độ trong suốt mà không phải tách chuỗi. */
  hue: number
  sat: number
}

/**
 * Bảy tông nền lấy cảm hứng từ ánh sáng trong rạp: đỏ rượu, xanh đêm,
 * tím rạp hát, lục rêu, hổ phách, chàm, mận chín.
 */
const PALETTE: { h: number; s: number }[] = [
  { h: 348, s: 46 }, { h: 212, s: 44 }, { h: 272, s: 38 },
  { h: 162, s: 34 }, { h: 32, s: 52 }, { h: 232, s: 40 }, { h: 316, s: 36 },
]

export function movieArt(seed: string): MovieArt {
  const n = hash(seed || 'cinebook')
  const base = PALETTE[n % PALETTE.length]
  // Lệch nhẹ tông thứ hai để nền không bao giờ là một màu chết.
  const shift = 18 + (n % 26)

  return {
    from: `hsl(${base.h} ${base.s}% 22%)`,
    to: `hsl(${(base.h + shift) % 360} ${Math.max(18, base.s - 14)}% 9%)`,
    accent: `hsl(${(base.h + 180) % 360} 62% 62%)`,
    glow: `hsl(${base.h} ${base.s + 14}% 52%)`,
    angle: 120 + (n % 90),
    initial: (seed.trim()[0] ?? '?').toUpperCase(),
    hue: base.h,
    sat: base.s,
  }
}

/**
 * Nền CSS dùng cho khối lớn: hero, đầu trang chi tiết phim, bìa phim.
 *
 * Độ trong suốt phải viết bằng cú pháp `hsl(h s% l% / a)`. Nối hậu tố alpha kiểu
 * hex vào chuỗi hsl() là CSS không hợp lệ, và chỉ cần MỘT lớp hỏng là trình duyệt
 * bỏ nguyên khai báo background — mất sạch cả ba lớp chứ không phải mất một lớp.
 */
export function artBackground(seed: string): string {
  const a = movieArt(seed)
  const glow = `hsl(${a.hue} ${a.sat + 14}% 52% / 0.22)`
  const accent = `hsl(${(a.hue + 180) % 360} 62% 62% / 0.15)`

  return [
    `radial-gradient(90% 120% at 12% 8%, ${glow} 0%, transparent 58%)`,
    `radial-gradient(70% 90% at 88% 22%, ${accent} 0%, transparent 60%)`,
    `linear-gradient(${a.angle}deg, ${a.from} 0%, ${a.to} 100%)`,
  ].join(', ')
}

/** Ảnh poster có phải chỉ là ảnh tạm của dữ liệu mẫu không. */
export const isPlaceholder = (url?: string | null) =>
  !url || url.includes('placehold.co')
