import type { Booking, Ticket } from '@/lib/types'
import { fullDateTime, money } from '@/lib/format'

/**
 * Dựng ảnh vé PNG từ mã QR đang hiển thị trên trang.
 *
 * Không thêm thư viện nào: mã QR vốn đã là một thẻ <svg> trong DOM, chỉ cần
 * tuần tự hoá rồi vẽ lên canvas cùng với thông tin suất chiếu. Khách tải về là
 * có tấm vé đầy đủ để đưa nhân viên soát, không phải mở lại website.
 */

const W = 420
const H = 620
const SCALE = 3   // xuất ở 3x cho nét khi phóng to hoặc in

const BRAND = '#E50914'
const INK = '#141414'
const MUTED = '#6B6B73'
const LINE = '#E2E2E6'

/** Vẽ SVG lên canvas qua một đối tượng Image trung gian. */
function svgToImage(svg: SVGElement): Promise<HTMLImageElement> {
  const xml = new XMLSerializer().serializeToString(svg)
  const url = URL.createObjectURL(new Blob([xml], { type: 'image/svg+xml;charset=utf-8' }))
  return new Promise((resolve, reject) => {
    const img = new Image()
    img.onload = () => { URL.revokeObjectURL(url); resolve(img) }
    img.onerror = (e) => { URL.revokeObjectURL(url); reject(e) }
    img.src = url
  })
}

/** Cắt bớt chuỗi cho vừa bề ngang, thêm dấu ba chấm. */
function fit(ctx: CanvasRenderingContext2D, text: string, maxWidth: number): string {
  if (ctx.measureText(text).width <= maxWidth) return text
  let cut = text
  while (cut.length > 1 && ctx.measureText(cut + '…').width > maxWidth) cut = cut.slice(0, -1)
  return cut + '…'
}

function line(ctx: CanvasRenderingContext2D, label: string, value: string, y: number) {
  ctx.font = '12px "Be Vietnam Pro", system-ui, sans-serif'
  ctx.fillStyle = MUTED
  ctx.textAlign = 'left'
  ctx.fillText(label, 28, y)

  ctx.font = '600 13px "Be Vietnam Pro", system-ui, sans-serif'
  ctx.fillStyle = INK
  ctx.textAlign = 'right'
  ctx.fillText(fit(ctx, value, 250), W - 28, y)
}

export async function buildTicketImage(
  svg: SVGElement, ticket: Ticket, booking: Booking,
): Promise<Blob> {
  // Chờ web font tải xong, nếu không canvas sẽ vẽ bằng font dự phòng và lệch chữ.
  try { await document.fonts.ready } catch { /* trình duyệt cũ */ }

  const canvas = document.createElement('canvas')
  canvas.width = W * SCALE
  canvas.height = H * SCALE
  const ctx = canvas.getContext('2d')
  if (!ctx) throw new Error('Trình duyệt không hỗ trợ canvas.')
  ctx.scale(SCALE, SCALE)

  // Nền và dải thương hiệu
  ctx.fillStyle = '#FFFFFF'
  ctx.fillRect(0, 0, W, H)
  ctx.fillStyle = BRAND
  ctx.fillRect(0, 0, W, 6)

  ctx.textBaseline = 'alphabetic'

  // Tên hệ thống
  ctx.font = '800 19px Bitter, Georgia, serif'
  ctx.fillStyle = INK
  ctx.textAlign = 'left'
  ctx.fillText('CineBook', 28, 44)

  ctx.font = '11px "Be Vietnam Pro", system-ui, sans-serif'
  ctx.fillStyle = MUTED
  ctx.textAlign = 'right'
  ctx.fillText('VÉ ĐIỆN TỬ', W - 28, 44)

  // Tên phim
  ctx.font = '800 21px Bitter, Georgia, serif'
  ctx.fillStyle = INK
  ctx.textAlign = 'left'
  ctx.fillText(fit(ctx, booking.movieTitle, W - 56), 28, 82)

  ctx.font = '12px "Be Vietnam Pro", system-ui, sans-serif'
  ctx.fillStyle = MUTED
  ctx.fillText(fit(ctx, `${booking.cinemaName} · ${booking.roomName}`, W - 56), 28, 103)

  // Mã QR
  const img = await svgToImage(svg)
  const qr = 210
  ctx.drawImage(img, (W - qr) / 2, 126, qr, qr)

  // Ghế nổi bật ngay dưới mã
  ctx.textAlign = 'center'
  ctx.font = '800 30px Bitter, Georgia, serif'
  ctx.fillStyle = INK
  ctx.fillText(ticket.seatLabel, W / 2, 380)

  ctx.font = '11px "JetBrains Mono", ui-monospace, monospace'
  ctx.fillStyle = MUTED
  ctx.fillText(ticket.code, W / 2, 400)

  // Đường xé
  ctx.strokeStyle = LINE
  ctx.lineWidth = 1
  ctx.setLineDash([5, 5])
  ctx.beginPath()
  ctx.moveTo(20, 424)
  ctx.lineTo(W - 20, 424)
  ctx.stroke()
  ctx.setLineDash([])

  // Khuyết hai bên cho giống cuống vé
  ctx.fillStyle = '#FFFFFF'
  for (const cx of [0, W]) {
    ctx.beginPath()
    ctx.arc(cx, 424, 9, 0, Math.PI * 2)
    ctx.fill()
    ctx.strokeStyle = LINE
    ctx.stroke()
  }

  // Thông tin chi tiết
  line(ctx, 'Suất chiếu', fullDateTime(booking.startsAtUtc), 456)
  line(ctx, 'Mã đặt vé', booking.code, 482)
  line(ctx, 'Ghế', `${ticket.seatLabel} (${booking.seats.length} vé trong đơn)`, 508)
  line(ctx, 'Tổng đơn', money(booking.total), 534)

  ctx.textAlign = 'center'
  ctx.font = '11px "Be Vietnam Pro", system-ui, sans-serif'
  ctx.fillStyle = MUTED
  ctx.fillText('Xuất trình mã QR này tại cửa phòng chiếu', W / 2, 578)

  ctx.strokeStyle = LINE
  ctx.strokeRect(0.5, 0.5, W - 1, H - 1)

  return new Promise((resolve, reject) => {
    canvas.toBlob((b) => b ? resolve(b) : reject(new Error('Không tạo được ảnh.')), 'image/png')
  })
}

/** Bảo trình duyệt tải một blob về máy. */
export function saveBlob(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  document.body.appendChild(a)
  a.click()
  a.remove()
  // Thu hồi muộn một nhịp, thu hồi ngay có trình duyệt huỷ luôn lượt tải.
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}

export const ticketFileName = (booking: Booking, ticket: Ticket) =>
  `CineBook-${booking.code}-${ticket.seatLabel}.png`
