import { useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { QRCodeSVG } from 'qrcode.react'
import { ChevronDown, Download, MapPin, Ticket as TicketIcon } from 'lucide-react'
import clsx from 'clsx'
import { api } from '@/lib/api'
import type { Booking, Ticket } from '@/lib/types'
import { BOOKING_STATUS, fullDateTime, money } from '@/lib/format'
import { artBackground } from '@/lib/art'
import { buildTicketImage, saveBlob, ticketFileName } from '@/lib/ticketImage'
import {
  Badge, Button, EmptyState, ErrorNote, SectionHeading, Skeleton, Spinner,
} from '@/components/ui'

export default function MyTickets() {
  const { data, isLoading } = useQuery({
    queryKey: ['bookings', 'mine'], queryFn: () => api.get<Booking[]>('/api/bookings/mine'),
  })

  return (
    <div className="mx-auto max-w-3xl px-4 py-10">
      <SectionHeading eyebrow="Tài khoản" title="Vé của tôi"
                      description="Mở mã QR ở đây khi tới cửa phòng chiếu." />

      {isLoading ? (
        <div className="flex flex-col gap-4">
          {[0, 1].map((i) => <Skeleton key={i} className="h-36 rounded-[14px]" />)}
        </div>
      ) : !data?.length ? (
        <EmptyState icon={<TicketIcon size={40} />} title="Bạn chưa có vé nào"
                    description="Chọn một bộ phim và đặt vé ngay."
                    action={<Link to="/phim"><Button>Xem phim đang chiếu</Button></Link>} />
      ) : (
        <div className="stagger flex flex-col gap-4">
          {data.map((b) => <BookingCard key={b.id} booking={b} />)}
        </div>
      )}
    </div>
  )
}

function BookingCard({ booking }: { booking: Booking }) {
  const [open, setOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState<string | null>(null)
  // Mỗi vé một ô chứa để lấy đúng thẻ <svg> mã QR của nó khi xuất ảnh.
  const qrRefs = useRef<Record<string, HTMLDivElement | null>>({})
  const status = BOOKING_STATUS[booking.status]
  const canShowQr = booking.status === 'Paid'

  const { data: tickets } = useQuery({
    queryKey: ['tickets', booking.id],
    queryFn: () => api.get<Ticket[]>(`/api/bookings/${booking.id}/tickets`),
    enabled: open && canShowQr,
  })

  /** Xuất một vé thành ảnh PNG rồi tải về. */
  const download = async (t: Ticket) => {
    const svg = qrRefs.current[t.id]?.querySelector('svg')
    if (!svg) { setError('Chưa tải xong mã QR, vui lòng thử lại.'); return }
    setSaving(t.id)
    setError(null)
    try {
      saveBlob(await buildTicketImage(svg, t, booking), ticketFileName(booking, t))
    } catch {
      setError('Không tạo được ảnh vé trên trình duyệt này.')
    } finally {
      setSaving(null)
    }
  }

  /** Đơn nhiều ghế: tải lần lượt, giãn nhịp để trình duyệt không chặn loạt tải. */
  const downloadAll = async (list: Ticket[]) => {
    for (const t of list) {
      await download(t)
      await new Promise((r) => setTimeout(r, 350))
    }
  }

  return (
    <div className="surface overflow-hidden">
      <div className="flex items-stretch">
        {/* Cuống vé bên trái, màu lấy theo tên phim nên mỗi vé một sắc riêng */}
        <div className="on-dark relative hidden w-16 shrink-0 sm:block"
             style={{ background: artBackground(booking.movieTitle) }}>
          <span className="absolute inset-0 grain opacity-40" aria-hidden />
          <span className="absolute inset-0 flex items-center justify-center">
            <span className="font-mono text-[10px] font-bold tracking-[0.3em] text-white/80"
                  style={{ writingMode: 'vertical-rl', transform: 'rotate(180deg)' }}>
              CINEBOOK
            </span>
          </span>
        </div>

        <span className="dashed-cut hidden w-px shrink-0 sm:block" />

        <div className="flex min-w-0 flex-1 items-start gap-4 p-5">
          <div className="min-w-0 flex-1">
            <div className="mb-2 flex flex-wrap items-center gap-2">
              <Badge tone={status.tone as 'brand'}>{status.label}</Badge>
              <span className="font-mono text-[12px] text-faint">{booking.code}</span>
            </div>
            <h3 className="font-display text-lg font-bold leading-snug">{booking.movieTitle}</h3>
            <p className="mt-1.5 flex items-start gap-1.5 text-[13px] text-muted">
              <MapPin size={13} className="mt-0.5 shrink-0 text-faint" />
              <span>{booking.cinemaName} · {booking.roomName} · {fullDateTime(booking.startsAtUtc)}</span>
            </p>
            <p className="mt-1.5 text-[13px] text-faint">
              Ghế <b className="font-mono text-cream">{booking.seats.map((s) => s.label).join(', ')}</b>
              {booking.combos.length > 0 &&
                ` · ${booking.combos.map((c) => `${c.name} ×${c.quantity}`).join(', ')}`}
            </p>
          </div>

          <div className="shrink-0 text-right">
            <p className="font-display text-lg font-extrabold text-brand-400 tabular">{money(booking.total)}</p>
            {canShowQr && (
              <button onClick={() => setOpen((v) => !v)}
                      className="mt-2 flex items-center gap-1 text-[12px] text-muted transition-colors hover:text-cream">
                {open ? 'Ẩn vé' : 'Xem mã QR'}
                <ChevronDown size={13} className={clsx('transition-transform', open && 'rotate-180')} />
              </button>
            )}
          </div>
        </div>
      </div>

      {open && canShowQr && (
        <div className="animate-rise border-t border-dashed border-ink-700 bg-ink-900 p-5">
          {!tickets ? <Spinner /> : (
            <>
              <div className="flex flex-wrap gap-4">
                {tickets.map((t) => (
                  <div key={t.id}
                       className="flex flex-col items-center gap-2 rounded-xl border border-ink-700 bg-ink-900 p-4">
                    <div ref={(el) => { qrRefs.current[t.id] = el }} className="rounded-lg bg-white p-2.5">
                      <QRCodeSVG value={t.qrPayload} size={116} level="M" />
                    </div>
                    <p className="font-mono text-sm font-bold">{t.seatLabel}</p>
                    <p className="font-mono text-[11px] text-faint">{t.code}</p>
                    {t.checkedInAtUtc && <Badge tone="mint">Đã vào rạp</Badge>}
                    <Button size="sm" variant="secondary" className="mt-1 w-full"
                            loading={saving === t.id} onClick={() => download(t)}>
                      <Download size={13} /> Tải vé
                    </Button>
                  </div>
                ))}
              </div>

              {error && <div className="mt-4"><ErrorNote>{error}</ErrorNote></div>}

              {tickets.length > 1 && (
                <div className="mt-4">
                  <Button size="sm" variant="secondary" disabled={saving !== null}
                          onClick={() => downloadAll(tickets)}>
                    <Download size={14} /> Tải tất cả {tickets.length} vé
                  </Button>
                </div>
              )}
            </>
          )}
        </div>
      )}
    </div>
  )
}
