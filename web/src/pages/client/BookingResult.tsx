import { Link, useParams, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { CheckCircle2, Clock3, QrCode, Ticket, XCircle } from 'lucide-react'
import { api } from '@/lib/api'
import type { Booking } from '@/lib/types'
import { fullDateTime, money, VNPAY_MESSAGE } from '@/lib/format'
import { Button, Card, Spinner } from '@/components/ui'

export default function BookingResult() {
  const { bookingId = '' } = useParams()
  const [params] = useSearchParams()

  // VNPay đưa khách về kèm mã kết quả; cổng giả lập thì không có tham số này.
  const vnpCode = params.get('vnp_ResponseCode')

  // Webhook có thể về sau đường redirect vài giây, nên hỏi lại cho tới khi đơn đổi trạng thái.
  const { data, isLoading } = useQuery({
    queryKey: ['booking', bookingId],
    queryFn: () => api.get<Booking>(`/api/bookings/${bookingId}`),
    refetchInterval: (q) => (q.state.data?.status === 'Pending' ? 1500 : false),
  })

  if (isLoading || !data) return <Spinner label="Đang kiểm tra kết quả thanh toán…" />

  const paid = data.status === 'Paid'
  // Cổng đã báo lỗi thì không phải "đang chờ" nữa, hiện thẳng lý do cho khách.
  const failedAtGateway = !paid && !!vnpCode && vnpCode !== '00'
  const pending = data.status === 'Pending' && !failedAtGateway

  return (
    <div className="mx-auto max-w-lg px-4 py-14">
      <Card className="overflow-hidden">
        {/* Cuống vé phía trên */}
        <div className="relative flex flex-col items-center gap-4 px-8 pb-7 pt-9 text-center">
          <div className="absolute inset-0 -z-10 screen-glow" />

          <span className={
            paid ? 'text-mint-400' : pending ? 'animate-pulse text-brand-400' : 'text-velvet-400'
          }>
            {paid ? <CheckCircle2 size={54} /> : pending ? <Clock3 size={54} /> : <XCircle size={54} />}
          </span>

          <div>
            <h1 className="font-display text-2xl font-extrabold">
              {paid ? 'Đặt vé thành công' : pending ? 'Đang chờ xác nhận' : 'Đơn hàng chưa hoàn tất'}
            </h1>
            <p className="mt-2 text-sm text-faint">
              {paid
                ? 'Vé điện tử đã sẵn sàng trong mục Vé của tôi.'
                : pending
                  ? 'Hệ thống đang chờ cổng thanh toán xác nhận. Trang sẽ tự cập nhật.'
                  : failedAtGateway
                    ? VNPAY_MESSAGE[vnpCode!] ?? 'Giao dịch không thành công.'
                    : 'Ghế đã được trả lại. Bạn có thể chọn lại suất chiếu khác.'}
            </p>
          </div>
        </div>

        {/* Đường xé vé */}
        <div className="relative flex items-center">
          <span className="h-5 w-5 shrink-0 -translate-x-1/2 rounded-full bg-ink-950" />
          <span className="dashed-cut h-px flex-1" />
          <span className="h-5 w-5 shrink-0 translate-x-1/2 rounded-full bg-ink-950" />
        </div>

        <div className="flex flex-col gap-5 px-8 pb-8 pt-6">
          <dl className="text-[13px]">
            <Line label="Mã đặt vé">
              <span className="font-mono text-base font-bold tracking-wider text-brand-400">{data.code}</span>
            </Line>
            <Line label="Phim"><b className="text-cream">{data.movieTitle}</b></Line>
            <Line label="Rạp">{data.cinemaName} · {data.roomName}</Line>
            <Line label="Suất chiếu">{fullDateTime(data.startsAtUtc)}</Line>
            <Line label="Ghế">
              <span className="font-mono font-semibold text-cream">
                {data.seats.map((s) => s.label).join(', ')}
              </span>
            </Line>
            {data.discountAmount > 0 && (
              <Line label="Giảm giá"><span className="text-mint-400">−{money(data.discountAmount)}</span></Line>
            )}
            <Line label="Tổng tiền">
              <b className="font-display text-lg text-cream tabular">{money(data.total)}</b>
            </Line>
          </dl>

          <div className="flex flex-wrap justify-center gap-3">
            {paid && (
              <Link to="/ve-cua-toi">
                <Button><QrCode size={15} /> Xem mã QR</Button>
              </Link>
            )}
            <Link to="/phim">
              <Button variant="secondary"><Ticket size={15} /> Tiếp tục đặt vé</Button>
            </Link>
          </div>
        </div>
      </Card>
    </div>
  )
}

function Line({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b border-ink-800 py-2.5 last:border-0">
      <dt className="shrink-0 text-faint">{label}</dt>
      <dd className="text-right">{children}</dd>
    </div>
  )
}
