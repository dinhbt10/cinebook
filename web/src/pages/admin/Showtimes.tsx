import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Ban, CalendarPlus, Power } from 'lucide-react'
import { api, ApiError } from '@/lib/api'
import type { Cinema, MovieListItem, Room, SeatTypePrices, Showtime } from '@/lib/types'
import { isoDateVN, money, ROOM_TYPE_LABEL, SUBTITLE_LABEL, time, utcFromVnInput } from '@/lib/format'
import { Badge, Button, Card, ErrorNote, Field, Input, Modal, Select, Spinner } from '@/components/ui'

export default function AdminShowtimes() {
  const qc = useQueryClient()
  const [date, setDate] = useState(() => isoDateVN(new Date()))
  const [cinemaId, setCinemaId] = useState('')
  const [open, setOpen] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const { data: cinemas } = useQuery({
    queryKey: ['cinemas'], queryFn: () => api.get<Cinema[]>('/api/cinemas'),
  })
  const { data: movies } = useQuery({
    queryKey: ['movies', 'NowShowing'],
    queryFn: () => api.get<MovieListItem[]>('/api/movies?status=NowShowing'),
  })
  const { data: showtimes, isLoading } = useQuery({
    queryKey: ['admin', 'showtimes', date, cinemaId],
    queryFn: () => api.get<Showtime[]>(
      `/api/showtimes?date=${date}${cinemaId ? `&cinemaId=${cinemaId}` : ''}`),
  })

  const cancel = useMutation({
    mutationFn: (id: string) => api.post(`/api/showtimes/${id}/cancel`,
      { reason: 'Hủy do sự cố kỹ thuật' }),
    onSuccess: () => { setError(null); qc.invalidateQueries({ queryKey: ['admin', 'showtimes'] }) },
    onError: (e: ApiError) => setError(e.message),
  })

  const toggleSale = useMutation({
    mutationFn: ({ id, value }: { id: string; value: boolean }) =>
      api.patch(`/api/showtimes/${id}/on-sale?value=${value}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'showtimes'] }),
    onError: (e: ApiError) => setError(e.message),
  })

  return (
    <div className="p-6 lg:p-8">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-extrabold">Suất chiếu</h1>
        <Button onClick={() => { setError(null); setOpen(true) }}>
          <CalendarPlus size={16} /> Tạo suất chiếu
        </Button>
      </div>

      <div className="mb-5 flex flex-wrap gap-3">
        <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} className="w-44" />
        <Select value={cinemaId} onChange={(e) => setCinemaId(e.target.value)} className="w-56">
          <option value="">Tất cả rạp</option>
          {cinemas?.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
        </Select>
      </div>

      {error && <div className="mb-4"><ErrorNote>{error}</ErrorNote></div>}

      {isLoading ? <Spinner /> : !showtimes?.length ? (
        <Card className="p-10 text-center text-[13px] text-faint">
          Không có suất chiếu nào trong ngày này.
        </Card>
      ) : (
        <Card className="overflow-x-auto">
          <table className="w-full min-w-[760px] text-sm">
            <thead>
              <tr className="border-b border-ink-700 text-left text-[11px] uppercase tracking-wide text-faint">
                <th className="px-5 py-3.5 font-semibold">Giờ</th>
                <th className="px-5 py-3.5 font-semibold">Phim</th>
                <th className="px-5 py-3.5 font-semibold">Rạp / phòng</th>
                <th className="px-5 py-3.5 font-semibold">Ghế</th>
                <th className="px-5 py-3.5 font-semibold">Giá từ</th>
                <th className="px-5 py-3.5" />
              </tr>
            </thead>
            <tbody>
              {showtimes.map((s) => {
                const sold = s.totalSeats - s.availableSeats
                const rate = s.totalSeats ? sold / s.totalSeats : 0
                return (
                  <tr key={s.id} className="border-b border-ink-800 last:border-0">
                    <td className="px-5 py-3">
                      <span className="font-display text-base font-bold tabular">{time(s.startsAtUtc)}</span>
                    </td>
                    <td className="px-5 py-3">
                      <p className="font-medium">{s.movieTitle}</p>
                      <p className="text-[12px] text-faint">{SUBTITLE_LABEL[s.subtitleMode]}</p>
                    </td>
                    <td className="px-5 py-3">
                      <p>{s.cinemaName}</p>
                      <p className="text-[12px] text-faint">
                        {s.roomName} · {ROOM_TYPE_LABEL[s.roomType] ?? s.roomType}
                      </p>
                    </td>
                    <td className="px-5 py-3">
                      <div className="flex items-center gap-2">
                        <div className="h-1.5 w-16 overflow-hidden rounded-full bg-ink-800">
                          <div className="h-full rounded-full bg-brand-500" style={{ width: `${rate * 100}%` }} />
                        </div>
                        <span className="text-[12px] text-faint tabular">{sold}/{s.totalSeats}</span>
                      </div>
                    </td>
                    <td className="px-5 py-3 tabular">{money(s.fromPrice)}</td>
                    <td className="px-5 py-3">
                      <div className="flex items-center justify-end gap-1">
                        <Badge tone={s.isOnSale ? 'mint' : 'faint'}>
                          {s.isOnSale ? 'Đang bán' : 'Ngừng bán'}
                        </Badge>
                        <button title={s.isOnSale ? 'Ngừng bán' : 'Mở bán'}
                          onClick={() => toggleSale.mutate({ id: s.id, value: !s.isOnSale })}
                          className="rounded-lg p-2 text-faint transition-colors hover:bg-ink-800 hover:text-cream">
                          <Power size={15} />
                        </button>
                        <button title="Hủy suất chiếu (hoàn 100% vé đã bán)"
                          onClick={() => cancel.mutate(s.id)}
                          className="rounded-lg p-2 text-faint transition-colors hover:bg-ink-800 hover:text-velvet-400">
                          <Ban size={15} />
                        </button>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </Card>
      )}

      <CreateShowtimeModal open={open} onClose={() => setOpen(false)}
                           cinemas={cinemas ?? []} movies={movies ?? []}
                           onCreated={() => qc.invalidateQueries({ queryKey: ['admin', 'showtimes'] })} />
    </div>
  )
}

const SEAT_PRICE_FIELDS: [keyof SeatTypePrices, string][] = [
  ['standard', 'Ghế thường'], ['vip', 'Ghế VIP'], ['couple', 'Ghế đôi'],
]

function CreateShowtimeModal({ open, onClose, cinemas, movies, onCreated }: {
  open: boolean; onClose: () => void
  cinemas: Cinema[]; movies: MovieListItem[]; onCreated: () => void
}) {
  const [movieId, setMovieId] = useState('')
  const [cinemaId, setCinemaId] = useState('')
  const [roomId, setRoomId] = useState('')
  const [startsLocal, setStartsLocal] = useState('')
  const [basePrice, setBasePrice] = useState(75000)
  // Chỉ giữ những ô quản trị đã sửa tay; ô còn lại lấy theo giá gợi ý.
  const [priceOverrides, setPriceOverrides] = useState<Partial<SeatTypePrices>>({})
  const [subtitleMode, setSubtitleMode] = useState('Subtitled')
  const [openForSale, setOpenForSale] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const { data: rooms } = useQuery({
    queryKey: ['rooms', cinemaId],
    queryFn: () => api.get<Room[]>(`/api/cinemas/${cinemaId}/rooms`),
    enabled: !!cinemaId,
  })

  /** Người dùng nhập giờ Việt Nam, backend nhận UTC. */
  const startsAtUtc = useMemo(() => (startsLocal ? utcFromVnInput(startsLocal) : ''), [startsLocal])

  // Giá gợi ý tính theo bảng giá ở backend (loại phòng, suất sáng, cuối tuần…).
  const { data: suggested } = useQuery({
    queryKey: ['price-preview', roomId, startsAtUtc, basePrice],
    queryFn: () => api.get<SeatTypePrices>(
      `/api/showtimes/price-preview?roomId=${roomId}` +
      `&startsAtUtc=${encodeURIComponent(startsAtUtc)}&basePrice=${basePrice}`),
    enabled: !!roomId && !!startsAtUtc && basePrice > 0,
  })

  const priceOf = (k: keyof SeatTypePrices) => priceOverrides[k] ?? suggested?.[k]

  const setPrice = (k: keyof SeatTypePrices, value: string) =>
    setPriceOverrides((o) => {
      const next = { ...o }
      // Xoá trắng ô thì quay về giá gợi ý.
      if (value === '') delete next[k]
      else next[k] = Number(value)
      return next
    })

  const create = useMutation({
    mutationFn: () => api.post('/api/showtimes', {
      movieId, roomId, startsAtUtc, basePrice, subtitleMode, openForSale,
      seatPrices: { standard: priceOf('standard'), vip: priceOf('vip'), couple: priceOf('couple') },
    }),
    onSuccess: () => { setError(null); setPriceOverrides({}); onCreated(); onClose() },
    onError: (e: ApiError) => setError(e.message),
  })

  return (
    <Modal open={open} onClose={onClose} title="Tạo suất chiếu">
      <form onSubmit={(e) => { e.preventDefault(); create.mutate() }} className="flex flex-col gap-4">
        {error && <ErrorNote>{error}</ErrorNote>}

        <Field label="Phim">
          <Select required value={movieId} onChange={(e) => setMovieId(e.target.value)}>
            <option value="">— Chọn phim —</option>
            {movies.map((m) => <option key={m.id} value={m.id}>{m.title} ({m.durationMinutes}′)</option>)}
          </Select>
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Rạp">
            <Select required value={cinemaId}
                    onChange={(e) => { setCinemaId(e.target.value); setRoomId('') }}>
              <option value="">— Chọn rạp —</option>
              {cinemas.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
          </Field>
          <Field label="Phòng chiếu">
            <Select required value={roomId} onChange={(e) => setRoomId(e.target.value)} disabled={!cinemaId}>
              <option value="">— Chọn phòng —</option>
              {rooms?.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name} · {ROOM_TYPE_LABEL[r.roomType] ?? r.roomType} · {r.seatCount} ghế
                </option>
              ))}
            </Select>
          </Field>
        </div>

        <Field label="Giờ bắt đầu (giờ Việt Nam)"
               hint="Giờ kết thúc tự tính theo thời lượng phim cộng thời gian dọn phòng">
          <Input type="datetime-local" required value={startsLocal}
                 onChange={(e) => setStartsLocal(e.target.value)} />
        </Field>

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Giá cơ sở (đồng)" hint="Dùng để tính giá gợi ý cho từng loại ghế bên dưới">
            <Input type="number" required min={1000} step={1000} value={basePrice}
                   onChange={(e) => setBasePrice(Number(e.target.value))} />
          </Field>
          <Field label="Ngôn ngữ">
            <Select value={subtitleMode} onChange={(e) => setSubtitleMode(e.target.value)}>
              <option value="Subtitled">Phụ đề</option>
              <option value="Dubbed">Lồng tiếng</option>
              <option value="Original">Nguyên bản</option>
            </Select>
          </Field>
        </div>

        <div className="rounded-xl border border-ink-700 bg-ink-900 p-3.5">
          <div className="mb-3 flex items-center justify-between gap-2">
            <p className="text-[13px] font-medium text-muted">Giá vé theo loại ghế (đồng)</p>
            {Object.keys(priceOverrides).length > 0 && (
              <button type="button" onClick={() => setPriceOverrides({})}
                      className="text-[12px] text-brand-400 hover:underline">
                Khôi phục giá gợi ý
              </button>
            )}
          </div>
          <div className="grid gap-3 sm:grid-cols-3">
            {SEAT_PRICE_FIELDS.map(([k, label]) => (
              <Field key={k} label={label}>
                <Input type="number" required min={1000} step={1000} value={priceOf(k) ?? ''}
                       onChange={(e) => setPrice(k, e.target.value)} />
              </Field>
            ))}
          </div>
          <p className="mt-2.5 text-[12px] text-faint">
            Tự điền theo bảng giá khi đã chọn phòng và giờ chiếu (tính cả loại phòng, suất sáng,
            cuối tuần). Sửa ô nào thì ô đó giữ giá bạn nhập — khách trả đúng giá ở đây.
          </p>
        </div>

        <label className="flex items-center gap-2.5 text-sm text-muted">
          <input type="checkbox" checked={openForSale} onChange={(e) => setOpenForSale(e.target.checked)}
                 className="h-4 w-4 accent-[#F0A62F]" />
          Mở bán vé ngay sau khi tạo
        </label>

        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="ghost" onClick={onClose}>Hủy</Button>
          <Button type="submit" loading={create.isPending}>Tạo suất chiếu</Button>
        </div>
      </form>
    </Modal>
  )
}
