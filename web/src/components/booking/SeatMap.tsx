import { useMemo } from 'react'
import clsx from 'clsx'
import type { SeatMapSeat } from '@/lib/types'
import { money, SEAT_TYPE_LABEL } from '@/lib/format'

interface Props {
  seats: SeatMapSeat[]
  selected: Set<string>
  onToggle: (seat: SeatMapSeat) => void
  maxSeats: number
}

/** Hàng từ 8 ghế trở lên thì chừa một lối đi ở giữa, đúng như bố trí rạp thật. */
const AISLE_FROM = 8

export function SeatMap({ seats, selected, onToggle, maxSeats }: Props) {
  const rows = useMemo(() => {
    const map = new Map<string, SeatMapSeat[]>()
    for (const s of seats) {
      const list = map.get(s.rowLabel) ?? []
      list.push(s)
      map.set(s.rowLabel, list)
    }
    return [...map.entries()]
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([label, list]) => [label, list.sort((a, b) => a.number - b.number)] as const)
  }, [seats])

  const atLimit = selected.size >= maxSeats

  return (
    // Khoang rạp luôn tối dù trang đang ở nền sáng: ghế và màn hình chỉ đọc được
    // khi có tương phản như trong phòng chiếu thật.
    <div className="on-dark relative overflow-hidden rounded-2xl bg-ink-950 px-3 py-7 sm:px-6">
      <div className="pointer-events-none absolute inset-0 screen-glow" aria-hidden />
      <div className="pointer-events-none absolute inset-0 grain opacity-25" aria-hidden />

      <div className="relative flex flex-col items-center">
        <CurvedScreen />

        <div className="w-full overflow-x-auto pb-2">
          <div className="mx-auto flex w-fit flex-col gap-1.5">
            {rows.map(([label, list]) => {
              const aisleAt = list.length >= AISLE_FROM ? Math.ceil(list.length / 2) : -1
              return (
                <div key={label} className="flex items-center gap-3">
                  <RowLabel>{label}</RowLabel>
                  <div className="flex items-center gap-1.5">
                    {list.map((seat, i) => (
                      <div key={seat.seatId} className="flex items-center">
                        {i === aisleAt && <span className="w-6 sm:w-8" aria-hidden />}
                        <Seat
                          seat={seat}
                          selected={selected.has(seat.seatId)}
                          blocked={atLimit && !selected.has(seat.seatId)}
                          onToggle={onToggle}
                        />
                      </div>
                    ))}
                  </div>
                  <RowLabel>{label}</RowLabel>
                </div>
              )
            })}
          </div>
        </div>

        <Legend />
      </div>
    </div>
  )
}

/**
 * Màn hình chiếu cong. Vẽ bằng một cung Bézier phình lên giữa, kèm chùm sáng
 * loe dần xuống phía khán giả — cho cảm giác chiều sâu mà một thanh gradient
 * thẳng không tạo được.
 */
function CurvedScreen() {
  return (
    <div className="mb-8 w-full max-w-2xl select-none">
      <svg viewBox="0 0 400 86" className="w-full" role="img" aria-label="Màn hình chiếu">
        <defs>
          <linearGradient id="cb-screen" x1="0" y1="0" x2="1" y2="0">
            <stop offset="0%" stopColor="var(--color-brand-500)" stopOpacity="0.15" />
            <stop offset="50%" stopColor="var(--color-brand-400)" stopOpacity="1" />
            <stop offset="100%" stopColor="var(--color-brand-500)" stopOpacity="0.15" />
          </linearGradient>
          <linearGradient id="cb-beam" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="var(--color-brand-500)" stopOpacity="0.30" />
            <stop offset="60%" stopColor="var(--color-brand-500)" stopOpacity="0.07" />
            <stop offset="100%" stopColor="var(--color-brand-500)" stopOpacity="0" />
          </linearGradient>
        </defs>

        {/* Ánh sáng hắt từ màn hình xuống chỗ ngồi */}
        <path d="M 30 18 Q 200 -6 370 18 L 400 86 L 0 86 Z" fill="url(#cb-beam)" />

        {/* Bản thân màn hình: cung cong phình lên giữa */}
        <path d="M 30 18 Q 200 -6 370 18" fill="none" stroke="url(#cb-screen)"
              strokeWidth="5" strokeLinecap="round" />
        {/* Viền mảnh phía dưới tạo độ dày cho màn hình */}
        <path d="M 30 23 Q 200 -1 370 23" fill="none" stroke="var(--color-brand-600)"
              strokeWidth="1.5" strokeLinecap="round" opacity="0.5" />
      </svg>

      <p className="-mt-6 text-center font-mono text-[11px] tracking-[0.4em] text-faint">
        MÀN HÌNH
      </p>
    </div>
  )
}

const RowLabel = ({ children }: { children: React.ReactNode }) => (
  <span className="w-4 shrink-0 text-center font-mono text-[11px] text-faint">{children}</span>
)

function Seat({ seat, selected, blocked, onToggle }: {
  seat: SeatMapSeat; selected: boolean; blocked: boolean; onToggle: (s: SeatMapSeat) => void
}) {
  const taken = seat.status === 'Sold' || seat.status === 'Maintenance'
             || (seat.status === 'Held' && !seat.heldByMe)
  const disabled = taken || (blocked && !selected)
  const couple = seat.seatType === 'Couple'

  const title = taken
    ? seat.status === 'Sold' ? `${seat.label} — đã bán`
      : seat.status === 'Maintenance' ? `${seat.label} — ghế bảo trì`
      : `${seat.label} — người khác đang giữ`
    : `${seat.label} · ${SEAT_TYPE_LABEL[seat.seatType]} · ${money(seat.price)}`

  return (
    <button
      type="button"
      title={title}
      aria-label={title}
      aria-pressed={selected}
      disabled={disabled}
      onClick={() => onToggle(seat)}
      className={clsx(
        // Bo trên nhiều hơn dưới cho giống lưng tựa của ghế rạp
        'relative h-7 rounded-t-lg rounded-b-[5px] border text-[10px] font-medium',
        'transition-all duration-150',
        couple ? 'w-[3.75rem]' : 'w-7',
        selected && 'scale-105 border-brand-400 bg-brand-500 text-on-brand shadow-[0_0_0_3px_rgba(229, 9, 20,.2)]',
        !selected && seat.status === 'Available' &&
          'border-ink-600 bg-ink-800 text-muted hover:-translate-y-0.5 hover:border-brand-500 hover:bg-ink-700 hover:text-cream',
        // Hổ phách chứ không phải đỏ: phải nhìn phát biết ngay đây không phải ghế mình chọn.
        !selected && seat.status === 'Held' && !seat.heldByMe &&
          'cursor-not-allowed border-amber-500/50 bg-amber-950 text-amber-400',
        seat.status === 'Sold' && 'cursor-not-allowed border-ink-700 bg-ink-700 text-ink-500',
        seat.status === 'Maintenance' &&
          'cursor-not-allowed border-dashed border-ink-600 bg-transparent text-ink-500',
        disabled && !taken && 'cursor-not-allowed opacity-35',
      )}
    >
      {seat.number}
      {/* Chấm nhỏ đánh dấu ghế VIP, khỏi phải tra bảng chú thích */}
      {seat.seatType === 'Vip' && !selected && seat.status === 'Available' && (
        <span aria-hidden
              className="absolute inset-x-0 -bottom-[3px] mx-auto h-[3px] w-3 rounded-full bg-brand-600/70" />
      )}
    </button>
  )
}

function Legend() {
  const items = [
    { cls: 'border-ink-600 bg-ink-800', label: 'Còn trống' },
    { cls: 'border-brand-400 bg-brand-500', label: 'Bạn đang chọn' },
    { cls: 'border-amber-500/50 bg-amber-950', label: 'Người khác giữ' },
    { cls: 'border-ink-700 bg-ink-700', label: 'Đã bán' },
    { cls: 'border-dashed border-ink-600 bg-transparent', label: 'Bảo trì' },
  ]
  return (
    <div className="mt-8 flex flex-wrap items-center justify-center gap-x-5 gap-y-2.5 border-t border-ink-800 pt-6">
      {items.map((i) => (
        <span key={i.label} className="flex items-center gap-2 text-[12px] text-faint">
          <span className={clsx('h-4 w-4 rounded-t-md rounded-b-sm border', i.cls)} />
          {i.label}
        </span>
      ))}
      <span className="flex items-center gap-2 text-[12px] text-faint">
        <span className="h-[3px] w-3 rounded-full bg-brand-600/70" /> Ghế VIP
      </span>
    </div>
  )
}
