import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { api } from '@/lib/api'
import type { MovieRevenue, RevenuePoint } from '@/lib/types'
import { isoDateVN, money } from '@/lib/format'
import { Card, Field, Input, Spinner } from '@/components/ui'

export default function AdminReports() {
  const [from, setFrom] = useState(() => {
    const d = new Date(); d.setDate(d.getDate() - 29); return isoDateVN(d)
  })
  const [to, setTo] = useState(() => isoDateVN(new Date()))
  const range = `from=${from}&to=${to}`

  const { data: byDay, isLoading } = useQuery({
    queryKey: ['report-day', range],
    queryFn: () => api.get<RevenuePoint[]>(`/api/reports/revenue-by-day?${range}`),
  })
  const { data: byMovie } = useQuery({
    queryKey: ['report-movie', range],
    queryFn: () => api.get<MovieRevenue[]>(`/api/reports/revenue-by-movie?${range}`),
  })

  const totalTicket = (byDay ?? []).reduce((s, r) => s + r.ticketRevenue, 0)
  const totalCombo = (byDay ?? []).reduce((s, r) => s + r.concessionRevenue, 0)

  return (
    <div className="p-6 lg:p-8">
      <h1 className="mb-6 text-2xl font-extrabold">Báo cáo doanh thu</h1>

      <div className="mb-6 flex flex-wrap items-end gap-4">
        <Field label="Từ ngày">
          <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-44" />
        </Field>
        <Field label="Đến ngày">
          <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-44" />
        </Field>
      </div>

      <div className="mb-6 grid gap-4 sm:grid-cols-3">
        <Card className="p-5">
          <p className="text-[12px] uppercase tracking-wide text-faint">Doanh thu vé</p>
          <p className="mt-2 font-display text-2xl font-extrabold tabular">{money(totalTicket)}</p>
        </Card>
        <Card className="p-5">
          <p className="text-[12px] uppercase tracking-wide text-faint">Doanh thu bắp nước</p>
          <p className="mt-2 font-display text-2xl font-extrabold tabular">{money(totalCombo)}</p>
        </Card>
        <Card className="p-5">
          <p className="text-[12px] uppercase tracking-wide text-faint">Tổng cộng</p>
          <p className="mt-2 font-display text-2xl font-extrabold text-brand-400 tabular">
            {money(totalTicket + totalCombo)}
          </p>
        </Card>
      </div>

      <div className="grid gap-5 lg:grid-cols-2">
        <Card className="overflow-hidden">
          <h2 className="border-b border-ink-700 px-5 py-4 text-base font-bold">Theo ngày</h2>
          {isLoading ? <Spinner /> : !byDay?.length ? (
            <p className="p-8 text-center text-[13px] text-faint">Chưa có dữ liệu trong khoảng này.</p>
          ) : (
            <div className="max-h-96 overflow-y-auto">
              <table className="w-full text-sm">
                <thead className="sticky top-0 bg-ink-850">
                  <tr className="border-b border-ink-700 text-left text-[11px] uppercase tracking-wide text-faint">
                    <th className="px-5 py-3 font-semibold">Ngày</th>
                    <th className="px-5 py-3 font-semibold">Đơn</th>
                    <th className="px-5 py-3 text-right font-semibold">Vé</th>
                    <th className="px-5 py-3 text-right font-semibold">Bắp nước</th>
                  </tr>
                </thead>
                <tbody>
                  {byDay.map((r) => (
                    <tr key={r.date} className="border-b border-ink-800 last:border-0">
                      <td className="px-5 py-2.5 tabular">{r.date.split('-').reverse().join('/')}</td>
                      <td className="px-5 py-2.5 tabular">{r.bookingCount}</td>
                      <td className="px-5 py-2.5 text-right tabular">{money(r.ticketRevenue)}</td>
                      <td className="px-5 py-2.5 text-right tabular">{money(r.concessionRevenue)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>

        <Card className="overflow-hidden">
          <h2 className="border-b border-ink-700 px-5 py-4 text-base font-bold">Theo phim</h2>
          {!byMovie?.length ? (
            <p className="p-8 text-center text-[13px] text-faint">Chưa có dữ liệu.</p>
          ) : (
            <div className="max-h-96 overflow-y-auto">
              <table className="w-full text-sm">
                <thead className="sticky top-0 bg-ink-850">
                  <tr className="border-b border-ink-700 text-left text-[11px] uppercase tracking-wide text-faint">
                    <th className="px-5 py-3 font-semibold">Phim</th>
                    <th className="px-5 py-3 text-right font-semibold">Vé</th>
                    <th className="px-5 py-3 text-right font-semibold">Lấp đầy</th>
                    <th className="px-5 py-3 text-right font-semibold">Doanh thu</th>
                  </tr>
                </thead>
                <tbody>
                  {byMovie.map((m) => (
                    <tr key={m.movieId} className="border-b border-ink-800 last:border-0">
                      <td className="max-w-40 truncate px-5 py-2.5">{m.title}</td>
                      <td className="px-5 py-2.5 text-right tabular">{m.ticketCount}</td>
                      <td className="px-5 py-2.5 text-right tabular">{(m.occupancyRate * 100).toFixed(0)}%</td>
                      <td className="px-5 py-2.5 text-right tabular">{money(m.revenue)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      </div>
    </div>
  )
}
