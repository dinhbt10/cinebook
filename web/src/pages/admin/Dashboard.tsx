import { useQuery } from '@tanstack/react-query'
import {
  Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis,
} from 'recharts'
import { Armchair, CircleDollarSign, MonitorPlay, Ticket } from 'lucide-react'
import { api } from '@/lib/api'
import type { Dashboard as DashboardDto, MovieRevenue, RevenuePoint } from '@/lib/types'
import { isoDateVN, money } from '@/lib/format'
import { Card, Spinner } from '@/components/ui'

export default function Dashboard() {
  const to = new Date()
  const from = new Date(); from.setDate(from.getDate() - 13)
  const range = `from=${isoDateVN(from)}&to=${isoDateVN(to)}`

  const { data: stats, isLoading } = useQuery({
    queryKey: ['dashboard'], queryFn: () => api.get<DashboardDto>('/api/reports/dashboard'),
  })
  const { data: revenue } = useQuery({
    queryKey: ['revenue-day', range],
    queryFn: () => api.get<RevenuePoint[]>(`/api/reports/revenue-by-day?${range}`),
  })
  const { data: byMovie } = useQuery({
    queryKey: ['revenue-movie', range],
    queryFn: () => api.get<MovieRevenue[]>(`/api/reports/revenue-by-movie?${range}`),
  })

  if (isLoading || !stats) return <Spinner label="Đang tải số liệu…" />

  const chart = (revenue ?? []).map((p) => ({
    date: p.date.slice(5).split('-').reverse().join('/'),
    total: p.ticketRevenue + p.concessionRevenue,
  }))

  return (
    <div className="p-6 lg:p-8">
      <h1 className="mb-6 text-2xl font-extrabold">Tổng quan</h1>

      <div className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <Stat icon={<CircleDollarSign size={17} />} label="Doanh thu hôm nay"
              value={money(stats.revenueToday)} sub={`Tháng này ${money(stats.revenueThisMonth)}`} />
        <Stat icon={<Ticket size={17} />} label="Vé bán hôm nay"
              value={String(stats.ticketsToday)} sub={`${stats.bookingsToday} đơn hàng`} />
        <Stat icon={<Armchair size={17} />} label="Tỷ lệ lấp đầy"
              value={`${(stats.occupancyToday * 100).toFixed(1)}%`} sub="ghế đã bán trên tổng ghế hôm nay" />
        <Stat icon={<MonitorPlay size={17} />} label="Suất chiếu hôm nay"
              value={String(stats.showtimesToday)} sub="đang mở bán" />
      </div>

      <Card className="mb-6 p-5">
        <h2 className="mb-5 text-base font-bold">Doanh thu 14 ngày gần nhất</h2>
        {chart.length === 0
          ? <p className="py-12 text-center text-[13px] text-faint">Chưa có dữ liệu doanh thu.</p>
          : (
            <div className="h-64 w-full">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={chart} margin={{ top: 4, right: 8, bottom: 0, left: 4 }}>
                  <defs>
                    <linearGradient id="rev" x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor="#F0A62F" stopOpacity={0.42} />
                      <stop offset="100%" stopColor="#F0A62F" stopOpacity={0} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid stroke="#29252F" vertical={false} />
                  <XAxis dataKey="date" tick={{ fill: '#736B7D', fontSize: 11 }}
                         axisLine={{ stroke: '#29252F' }} tickLine={false} />
                  <YAxis tick={{ fill: '#736B7D', fontSize: 11 }} axisLine={false} tickLine={false}
                         width={46} tickFormatter={(v) => `${Math.round(v / 1_000_000)}tr`} />
                  <Tooltip
                    contentStyle={{ background: '#17151A', border: '1px solid #29252F',
                                    borderRadius: 12, fontSize: 13, color: '#F4F1F5' }}
                    labelStyle={{ color: '#A79FB0' }}
                    formatter={(v) => [money(Number(v)), 'Doanh thu'] as [string, string]} />
                  <Area type="monotone" dataKey="total" stroke="#F0A62F" strokeWidth={2} fill="url(#rev)" />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          )}
      </Card>

      <Card className="p-5">
        <h2 className="mb-4 text-base font-bold">Phim bán chạy</h2>
        {!byMovie?.length
          ? <p className="py-8 text-center text-[13px] text-faint">Chưa có dữ liệu.</p>
          : (
            <div className="flex flex-col gap-3">
              {byMovie.slice(0, 6).map((m, i) => (
                <div key={m.movieId} className="flex items-center gap-4">
                  <span className="w-5 shrink-0 text-center font-mono text-[12px] text-faint">{i + 1}</span>
                  <div className="min-w-0 flex-1">
                    <div className="flex items-baseline justify-between gap-3">
                      <p className="truncate text-sm font-medium">{m.title}</p>
                      <p className="shrink-0 text-[13px] font-semibold text-brand-400 tabular">{money(m.revenue)}</p>
                    </div>
                    <div className="mt-1.5 flex items-center gap-3">
                      <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-ink-800">
                        <div className="h-full rounded-full bg-brand-500"
                             style={{ width: `${Math.max(3, m.occupancyRate * 100)}%` }} />
                      </div>
                      <span className="shrink-0 text-[11px] text-faint tabular">
                        {m.ticketCount} vé · lấp đầy {(m.occupancyRate * 100).toFixed(0)}%
                      </span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
      </Card>
    </div>
  )
}

function Stat({ icon, label, value, sub }: {
  icon: React.ReactNode; label: string; value: string; sub: string
}) {
  return (
    <Card className="p-5">
      <div className="mb-3 flex items-center gap-2 text-faint">
        <span className="text-brand-500">{icon}</span>
        <span className="text-[12px] font-medium uppercase tracking-wide">{label}</span>
      </div>
      <p className="font-display text-2xl font-extrabold tabular">{value}</p>
      <p className="mt-1 text-[12px] text-faint">{sub}</p>
    </Card>
  )
}
