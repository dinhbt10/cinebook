import { useCallback, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  ArrowLeft, ArrowRight, Check, CreditCard, Minus, Plus, Popcorn,
  QrCode, RefreshCw, ShieldCheck, Tag, Ticket, Landmark, Globe,
} from 'lucide-react'
import clsx from 'clsx'
import { api, ApiError } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import type {
  Booking as BookingDto, Combo, PaymentProvider, SeatMap as SeatMapDto, SeatMapSeat,
} from '@/lib/types'
import { fullDateTime, money, SEAT_TYPE_LABEL } from '@/lib/format'
import { SeatMap } from '@/components/booking/SeatMap'
import { VoucherPicker } from '@/components/booking/VoucherPicker'
import { Countdown } from '@/components/booking/Countdown'
import { Button, Card, ErrorNote, Spinner } from '@/components/ui'

type Step = 'seats' | 'extras' | 'confirm'

const STEPS: { key: Step; label: string }[] = [
  { key: 'seats', label: 'Chọn ghế' },
  { key: 'extras', label: 'Bắp nước & ưu đãi' },
  { key: 'confirm', label: 'Thanh toán' },
]

export default function Booking() {
  const { showtimeId = '' } = useParams()
  const navigate = useNavigate()
  const { user } = useAuth()
  const qc = useQueryClient()

  const [step, setStep] = useState<Step>('seats')
  const [selected, setSelected] = useState<Set<string>>(new Set())
  const [comboQty, setComboQty] = useState<Record<string, number>>({})
  const [promoCode, setPromoCode] = useState('')
  const [booking, setBooking] = useState<BookingDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [provider, setProvider] = useState<string | null>(null)
  const [method, setMethod] = useState<string | null>(null)

  // Gọi lại sơ đồ ghế mỗi 5 giây — bản đơn giản thay cho WebSocket.
  const { data: seatMap, isLoading, refetch, isFetching } = useQuery({
    queryKey: ['seatmap', showtimeId],
    queryFn: () => api.get<SeatMapDto>(`/api/showtimes/${showtimeId}/seatmap`),
    refetchInterval: step === 'seats' ? 5000 : false,
  })

  const { data: combos } = useQuery({
    queryKey: ['combos'], queryFn: () => api.get<Combo[]>('/api/combos'),
  })

  // Máy chủ quyết định cổng nào đang bật: cổng thiếu cấu hình sẽ không hiện ra.
  const { data: providersRaw } = useQuery({
    queryKey: ['payment-providers'],
    queryFn: () => api.get<PaymentProvider[]>('/api/payments/providers'),
    staleTime: Infinity,
  })

  // Nếu máy chủ chạy bản cũ chưa có endpoint này, SPA fallback trả về HTML.
  // Không kiểm tra kiểu ở đây thì cả trang đặt vé trắng vì gọi .map trên chuỗi.
  const providers = Array.isArray(providersRaw) ? providersRaw : []
  const activeProvider = providers.find((g) => g.provider === provider) ?? providers[0]
  const chosenProvider = activeProvider?.provider ?? 'Mock'
  // Không chọn gì thì lấy cách đầu tiên — với VNPay đó là quét mã QR.
  const chosenMethod = method ?? activeProvider?.methods?.[0]?.code ?? ''

  const seatById = useMemo(
    () => new Map((seatMap?.seats ?? []).map((s) => [s.seatId, s])),
    [seatMap],
  )
  const chosen = useMemo(
    () => [...selected].map((id) => seatById.get(id)).filter(Boolean) as SeatMapSeat[],
    [selected, seatById],
  )
  const seatsTotal = chosen.reduce((sum, s) => sum + s.price, 0)
  const combosTotal = (combos ?? []).reduce((sum, c) => sum + c.price * (comboQty[c.id] ?? 0), 0)

  const toggleSeat = useCallback((seat: SeatMapSeat) => {
    setError(null)
    setSelected((prev) => {
      const next = new Set(prev)
      if (next.has(seat.seatId)) next.delete(seat.seatId)
      else if (next.size < (seatMap?.maxSeatsPerBooking ?? 8)) next.add(seat.seatId)
      return next
    })
  }, [seatMap?.maxSeatsPerBooking])

  const hold = useMutation({
    mutationFn: () => api.post<BookingDto>('/api/bookings/hold', {
      showtimeId,
      seatIds: [...selected],
      combos: Object.entries(comboQty)
        .filter(([, q]) => q > 0)
        .map(([comboId, quantity]) => ({ comboId, quantity })),
      promotionCode: promoCode.trim() || null,
    }),
    onSuccess: (b) => { setBooking(b); setStep('confirm'); setError(null) },
    onError: (e: ApiError) => {
      setError(e.message)
      if (e.code === 'SEATS_TAKEN') { setSelected(new Set()); setStep('seats'); refetch() }
    },
  })

  const pay = useMutation({
    mutationFn: () => api.post<{ paymentUrl: string }>(`/api/bookings/${booking!.id}/payments`, {
      provider: chosenProvider,
      methodCode: chosenMethod,
      returnUrl: `${location.origin}/dat-ve/ket-qua/${booking!.id}`,
    }),
    onSuccess: (r) => { location.href = r.paymentUrl },
    onError: (e: ApiError) => setError(e.message),
  })

  const cancelHold = useMutation({
    mutationFn: () => api.post(`/api/bookings/${booking!.id}/cancel`),
    onSuccess: () => {
      setBooking(null); setSelected(new Set()); setStep('seats')
      qc.invalidateQueries({ queryKey: ['seatmap', showtimeId] })
    },
  })

  if (isLoading) return <Spinner label="Đang tải sơ đồ ghế…" />
  if (!seatMap) return <div className="mx-auto max-w-6xl px-4 py-16 text-center text-faint">Không tìm thấy suất chiếu.</div>

  if (!user) {
    return (
      <div className="mx-auto max-w-md px-4 py-20 text-center">
        <Ticket size={40} className="mx-auto mb-4 text-brand-500" />
        <h1 className="mb-2 text-2xl font-bold">Đăng nhập để đặt vé</h1>
        <p className="mb-6 text-sm text-faint">Bạn cần đăng nhập trước khi chọn ghế cho suất chiếu này.</p>
        <Button onClick={() => navigate(`/dang-nhap?next=/dat-ve/${showtimeId}`)}>Đăng nhập</Button>
      </div>
    )
  }

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      {/* Tiêu đề suất chiếu */}
      <div className="mb-7">
        <button onClick={() => navigate(-1)}
                className="mb-3 flex items-center gap-1.5 text-[13px] text-faint transition-colors hover:text-cream">
          <ArrowLeft size={14} /> Quay lại
        </button>
        <h1 className="font-display text-2xl font-extrabold md:text-3xl">{seatMap.movieTitle}</h1>
        <p className="mt-1.5 text-sm text-muted">
          {seatMap.cinemaName} · {seatMap.roomName} · {fullDateTime(seatMap.startsAtUtc)}
        </p>
      </div>

      {/* Các bước */}
      <ol className="mb-8 flex items-center text-[13px]">
        {STEPS.map((s, i) => {
          const index = STEPS.findIndex((x) => x.key === step)
          const state = i < index ? 'done' : i === index ? 'active' : 'todo'
          return (
            <li key={s.key} className={clsx('flex items-center gap-2.5', i > 0 && 'flex-1')}>
              {i > 0 && (
                <span className="h-px flex-1 bg-ink-700">
                  <span className={clsx('block h-px bg-brand-500 transition-all duration-500',
                    state === 'todo' ? 'w-0' : 'w-full')} />
                </span>
              )}
              <span className={clsx(
                'flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-[11px] font-bold transition-all',
                state === 'active' && 'bg-brand-500 text-on-brand shadow-[0_0_0_4px_rgba(229, 9, 20,.15)]',
                state === 'done' && 'bg-mint-950 text-mint-400',
                state === 'todo' && 'border border-ink-600 text-faint',
              )}>
                {state === 'done' ? <Check size={13} /> : i + 1}
              </span>
              <span className={clsx('hidden sm:inline', state === 'todo' ? 'text-faint' : 'text-cream')}>
                {s.label}
              </span>
            </li>
          )
        })}
      </ol>

      <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
        {/* ---------------- Nội dung từng bước ---------------- */}
        <div className="min-w-0">
          {step === 'seats' && (
            <Card className="p-5 sm:p-7">
              <div className="mb-2 flex items-center justify-between">
                <p className="text-[13px] text-faint">
                  Tối đa {seatMap.maxSeatsPerBooking} ghế mỗi lần đặt
                </p>
                <button onClick={() => refetch()}
                        className="flex items-center gap-1.5 text-[12px] text-faint transition-colors hover:text-cream">
                  <RefreshCw size={12} className={isFetching ? 'animate-spin' : ''} /> Làm mới
                </button>
              </div>
              <SeatMap seats={seatMap.seats} selected={selected}
                       onToggle={toggleSeat} maxSeats={seatMap.maxSeatsPerBooking} />
            </Card>
          )}

          {step === 'extras' && (
            <div className="flex flex-col gap-5">
              <Card className="p-5 sm:p-6">
                <div className="mb-4 flex items-center gap-2">
                  <Popcorn size={17} className="text-brand-500" />
                  <h2 className="text-lg font-bold">Bắp nước</h2>
                </div>
                <div className="flex flex-col gap-3">
                  {combos?.map((c) => {
                    const qty = comboQty[c.id] ?? 0
                    return (
                      <div key={c.id} className="flex items-center gap-4 rounded-xl border border-ink-700 bg-ink-900 p-3.5">
                        <div className="min-w-0 flex-1">
                          <p className="text-sm font-medium">{c.name}</p>
                          {c.description && <p className="mt-0.5 text-[12px] text-faint">{c.description}</p>}
                          <p className="mt-1 text-[13px] font-semibold text-brand-400 tabular">{money(c.price)}</p>
                        </div>
                        <div className="flex shrink-0 items-center gap-1">
                          <QtyButton onClick={() => setComboQty((s) => ({ ...s, [c.id]: Math.max(0, qty - 1) }))}
                                     disabled={qty === 0}><Minus size={13} /></QtyButton>
                          <span className="w-7 text-center text-sm font-semibold tabular">{qty}</span>
                          <QtyButton onClick={() => setComboQty((s) => ({ ...s, [c.id]: Math.min(9, qty + 1) }))}
                                     disabled={qty >= 9}><Plus size={13} /></QtyButton>
                        </div>
                      </div>
                    )
                  })}
                </div>
              </Card>

              <Card className="p-5 sm:p-6">
                <div className="mb-1 flex items-center gap-2">
                  <Tag size={17} className="text-brand-500" />
                  <h2 className="text-lg font-bold">Voucher</h2>
                </div>
                <p className="mb-4 text-[13px] text-faint">
                  Bấm chọn voucher, số tiền giảm hiện sẵn theo đúng đơn của bạn.
                </p>
                <VoucherPicker
                  amount={seatsTotal + combosTotal}
                  value={promoCode || null}
                  onChange={(code) => setPromoCode(code ?? '')}
                />
              </Card>
            </div>
          )}

          {step === 'confirm' && booking && (
            <Card className="p-5 sm:p-7">
              <h2 className="mb-1 text-lg font-bold">Xác nhận và thanh toán</h2>
              <p className="mb-5 text-[13px] text-faint">
                Ghế đã được giữ cho bạn. Hoàn tất thanh toán trước khi hết giờ, nếu không ghế sẽ được trả lại cho người khác.
              </p>

              <dl className="flex flex-col gap-2.5 text-sm">
                <Row label="Mã đặt vé"><span className="font-mono font-bold text-brand-400">{booking.code}</span></Row>
                <Row label="Phim">{booking.movieTitle}</Row>
                <Row label="Rạp">{booking.cinemaName} · {booking.roomName}</Row>
                <Row label="Suất chiếu">{fullDateTime(booking.startsAtUtc)}</Row>
                <Row label="Ghế">
                  {booking.seats.map((s) => s.label).join(', ')}
                </Row>
              </dl>

              {/* Chọn cổng thanh toán, rồi chọn cách trả tiền trong cổng đó */}
              {providers.length > 0 && (
                <div className="mt-6 flex flex-col gap-5">
                  <div>
                    <p className="mb-3 text-[13px] font-semibold text-muted">Cổng thanh toán</p>
                    <div className="grid gap-2.5 sm:grid-cols-2">
                      {providers.map((g) => {
                        const active = g.provider === chosenProvider
                        return (
                          <button key={g.provider} type="button" aria-pressed={active}
                            onClick={() => { setProvider(g.provider); setMethod(null) }}
                            className={clsx(
                              'flex items-center gap-3 rounded-xl border px-4 py-3 text-left transition-all',
                              active
                                ? 'border-brand-500 bg-brand-950/60'
                                : 'border-ink-600 bg-ink-900 hover:border-ink-500',
                            )}>
                            <span className={clsx(
                              'flex h-9 w-9 shrink-0 items-center justify-center rounded-lg',
                              active ? 'bg-brand-500 text-on-brand' : 'bg-ink-800 text-muted',
                            )}>
                              {g.provider === 'VNPay' ? <QrCode size={16} /> : <CreditCard size={16} />}
                            </span>
                            <span className="min-w-0">
                              <span className="block truncate text-sm font-medium">{g.displayName}</span>
                              <span className="block text-[11px] text-faint">
                                {g.provider === 'VNPay'
                                  ? 'Quét mã QR, thẻ ATM hoặc thẻ quốc tế'
                                  : 'Mô phỏng cho môi trường học tập'}
                              </span>
                            </span>
                            {active && <Check size={16} className="ml-auto shrink-0 text-brand-400" />}
                          </button>
                        )
                      })}
                    </div>
                  </div>

                  {/* Cổng nào có nhiều cách trả tiền thì cho chọn tiếp */}
                  {(activeProvider?.methods?.length ?? 0) > 1 && (
                    <div>
                      <p className="mb-3 text-[13px] font-semibold text-muted">Cách thanh toán</p>
                      <div className="flex flex-wrap gap-2">
                        {activeProvider!.methods.map((m) => {
                          const active = m.code === chosenMethod
                          const Icon = m.code === 'VNPAYQR' ? QrCode
                                     : m.code === 'VNBANK' ? Landmark
                                     : m.code === 'INTCARD' ? Globe
                                     : CreditCard
                          return (
                            <button key={m.code || 'auto'} type="button" aria-pressed={active}
                              onClick={() => setMethod(m.code)}
                              className={clsx(
                                'flex items-center gap-2 rounded-xl border px-3.5 py-2 text-[13px] transition-all',
                                active
                                  ? 'border-brand-500 bg-brand-500 font-semibold text-on-brand'
                                  : 'border-ink-600 bg-ink-900 text-muted hover:border-ink-500 hover:text-cream',
                              )}>
                              <Icon size={14} /> {m.displayName}
                            </button>
                          )
                        })}
                      </div>
                      {chosenMethod === 'VNPAYQR' && (
                        <p className="mt-3 flex items-start gap-1.5 rounded-xl border border-ink-700 bg-ink-900 px-3.5 py-2.5 text-[12px] text-muted">
                          <QrCode size={14} className="mt-0.5 shrink-0 text-brand-400" />
                          Bấm thanh toán để sang trang VNPay, mã QR hiện ở đó. Mở app ngân hàng
                          hoặc ví điện tử, quét mã rồi xác nhận — trang kết quả tự cập nhật.
                        </p>
                      )}
                    </div>
                  )}

                  <p className="flex items-start gap-1.5 text-[12px] text-faint">
                    <ShieldCheck size={13} className="mt-0.5 shrink-0 text-mint-400" />
                    Kết quả thanh toán được xác nhận bằng chữ ký từ cổng, hệ thống chỉ ghi nhận một lần.
                  </p>
                </div>
              )}

              {error && <div className="mt-5"><ErrorNote>{error}</ErrorNote></div>}

              <div className="mt-6 flex flex-wrap gap-3">
                <Button size="lg" loading={pay.isPending} onClick={() => pay.mutate()}>
                  <CreditCard size={16} /> Thanh toán {money(booking.total)}
                </Button>
                <Button size="lg" variant="ghost" loading={cancelHold.isPending}
                        onClick={() => cancelHold.mutate()}>
                  Hủy và chọn lại ghế
                </Button>
              </div>
            </Card>
          )}
        </div>

        {/* ---------------- Tóm tắt bên phải ---------------- */}
        <aside className="lg:sticky lg:top-24 lg:self-start">
          <Card className="flex flex-col gap-4 p-5">
            {booking && step === 'confirm' && (
              <Countdown expiresAtUtc={booking.holdExpiresAtUtc}
                         onExpire={() => { setBooking(null); setStep('seats'); setSelected(new Set()); refetch() }} />
            )}

            <div>
              <p className="mb-2 text-[13px] font-semibold text-muted">Ghế đã chọn</p>
              {chosen.length === 0
                ? <p className="text-[13px] text-faint">Chưa chọn ghế nào</p>
                : (
                  <ul className="flex flex-col gap-1.5">
                    {chosen.map((s) => (
                      <li key={s.seatId} className="flex items-center justify-between text-[13px]">
                        <span>
                          <b className="font-mono">{s.label}</b>
                          <span className="ml-1.5 text-faint">{SEAT_TYPE_LABEL[s.seatType]}</span>
                        </span>
                        <span className="tabular">{money(s.price)}</span>
                      </li>
                    ))}
                  </ul>
                )}
            </div>

            {combosTotal > 0 && (
              <div className="border-t border-ink-700 pt-3">
                <p className="mb-2 text-[13px] font-semibold text-muted">Bắp nước</p>
                <ul className="flex flex-col gap-1.5">
                  {combos?.filter((c) => (comboQty[c.id] ?? 0) > 0).map((c) => (
                    <li key={c.id} className="flex items-center justify-between gap-3 text-[13px]">
                      <span className="min-w-0 truncate">{c.name} × {comboQty[c.id]}</span>
                      <span className="shrink-0 tabular">{money(c.price * comboQty[c.id])}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            <div className="border-t border-ink-700 pt-3 text-[13px]">
              {booking ? (
                <>
                  <SumRow label="Tiền ghế" value={booking.seatsSubtotal} />
                  {booking.concessionSubtotal > 0 && <SumRow label="Bắp nước" value={booking.concessionSubtotal} />}
                  {booking.discountAmount > 0 && (
                    <SumRow label="Giảm giá" value={-booking.discountAmount} tone="mint" />
                  )}
                  <div className="mt-2.5 flex items-center justify-between border-t border-ink-700 pt-2.5">
                    <span className="font-semibold">Tổng thanh toán</span>
                    <span className="font-display text-xl font-extrabold text-brand-400 tabular">{money(booking.total)}</span>
                  </div>
                </>
              ) : (
                <>
                  <SumRow label="Tiền ghế" value={seatsTotal} />
                  {combosTotal > 0 && <SumRow label="Bắp nước" value={combosTotal} />}
                  <div className="mt-2.5 flex items-center justify-between border-t border-ink-700 pt-2.5">
                    <span className="font-semibold">Tạm tính</span>
                    <span className="font-display text-xl font-extrabold text-brand-400 tabular">
                      {money(seatsTotal + combosTotal)}
                    </span>
                  </div>
                </>
              )}
            </div>

            {error && step !== 'confirm' && <ErrorNote>{error}</ErrorNote>}

            {step === 'seats' && (
              <Button size="lg" disabled={chosen.length === 0} onClick={() => setStep('extras')}>
                Tiếp tục <ArrowRight size={16} />
              </Button>
            )}
            {step === 'extras' && (
              <div className="flex flex-col gap-2">
                <Button size="lg" loading={hold.isPending} onClick={() => hold.mutate()}>
                  Giữ ghế và tiếp tục <ArrowRight size={16} />
                </Button>
                <Button variant="ghost" onClick={() => setStep('seats')}>Quay lại chọn ghế</Button>
              </div>
            )}
          </Card>
        </aside>
      </div>
    </div>
  )
}

function QtyButton({ children, ...rest }: React.ButtonHTMLAttributes<HTMLButtonElement>) {
  return (
    <button {...rest}
      className="flex h-7 w-7 items-center justify-center rounded-lg border border-ink-600 text-muted
                 transition-colors hover:border-ink-500 hover:text-cream disabled:opacity-35">
      {children}
    </button>
  )
}

function Row({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-4">
      <dt className="shrink-0 text-faint">{label}</dt>
      <dd className="text-right font-medium">{children}</dd>
    </div>
  )
}

function SumRow({ label, value, tone }: { label: string; value: number; tone?: 'mint' }) {
  return (
    <div className="flex items-center justify-between py-0.5">
      <span className="text-faint">{label}</span>
      <span className={clsx('tabular', tone === 'mint' && 'text-mint-400')}>{money(value)}</span>
    </div>
  )
}
