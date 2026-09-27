import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  ArrowRight, CalendarDays, Clock, MapPin, MousePointerClick,
  Play, QrCode, Sparkles, Tag, Ticket, Timer,
} from 'lucide-react'
import clsx from 'clsx'
import { api } from '@/lib/api'
import type { Cinema, MovieListItem, Promotion, Showtime } from '@/lib/types'
import { AGE_RATING_LABEL, isoDateVN, money, ROOM_TYPE_LABEL, time } from '@/lib/format'
import { MovieCard } from '@/components/MovieCard'
import { Backdrop, Poster } from '@/components/Poster'
import { TrailerModal } from '@/components/TrailerModal'
import {
  Badge, Button, PosterGridSkeleton, SectionHeading, Skeleton, StatTile,
} from '@/components/ui'

export default function Home() {
  const [tab, setTab] = useState<'NowShowing' | 'ComingSoon'>('NowShowing')
  const [heroIndex, setHeroIndex] = useState(0)
  const [trailer, setTrailer] = useState<MovieListItem | null>(null)
  const today = useMemo(() => isoDateVN(new Date()), [])

  const { data: nowShowing, isLoading } = useQuery({
    queryKey: ['movies', 'NowShowing'],
    queryFn: () => api.get<MovieListItem[]>('/api/movies?status=NowShowing'),
  })
  const { data: coming } = useQuery({
    queryKey: ['movies', 'ComingSoon'],
    queryFn: () => api.get<MovieListItem[]>('/api/movies?status=ComingSoon'),
  })
  const { data: promos } = useQuery({
    queryKey: ['promotions'],
    queryFn: () => api.get<Promotion[]>('/api/promotions'),
  })
  const { data: cinemas } = useQuery({
    queryKey: ['cinemas', ''],
    queryFn: () => api.get<Cinema[]>('/api/cinemas'),
  })
  const { data: todayShows } = useQuery({
    queryKey: ['showtimes', 'today', today],
    queryFn: () => api.get<Showtime[]>(`/api/showtimes?date=${today}`),
  })

  const spotlight = useMemo(() => (nowShowing ?? []).slice(0, 3), [nowShowing])
  const featured = spotlight[heroIndex] ?? spotlight[0]
  const list = tab === 'NowShowing' ? nowShowing : coming

  // Hero tự chuyển phim, để khung hình lớn không đứng yên một chỗ.
  useEffect(() => {
    if (spotlight.length < 2) return
    const t = setInterval(() => setHeroIndex((i) => (i + 1) % spotlight.length), 7000)
    return () => clearInterval(t)
  }, [spotlight.length])

  return (
    <div>
      {/* ══════════════════════════ Hero ══════════════════════════ */}
      <section className="on-dark relative isolate overflow-hidden border-b border-ink-800">
        {featured ? (
          <>
            <div key={featured.id} className="absolute inset-0 -z-20 animate-drift">
              <Backdrop title={featured.title} backdropUrl={featured.backdropUrl} />
            </div>
            <div className="absolute inset-0 -z-10 scrim-b" />
            <div className="absolute inset-0 -z-10 hidden scrim-l md:block" />
            <div className="absolute inset-0 -z-10 grain opacity-30" />

            <div className="relative mx-auto grid max-w-6xl gap-8 px-4 py-14 md:grid-cols-[240px_1fr] md:py-20">
              <Link to={`/phim/${featured.id}`}
                    className="mx-auto w-40 overflow-hidden rounded-2xl border border-ink-700/80 shadow-2xl shadow-ink-950 md:mx-0 md:w-full">
                <div className="aspect-[2/3] w-full">
                  <Poster title={featured.title} posterUrl={featured.posterUrl} eager />
                </div>
              </Link>

              <div key={featured.id} className="flex animate-rise flex-col justify-center gap-5 text-center md:text-left">
                <span className="mx-auto inline-flex w-fit items-center gap-1.5 rounded-full border border-brand-600/40 bg-brand-950/80 px-3 py-1 text-[12px] font-medium text-brand-400 backdrop-blur md:mx-0">
                  <Sparkles size={12} /> Phim nổi bật tuần này
                </span>

                <h1 className="font-display text-4xl font-extrabold leading-[1.05] md:text-6xl">
                  {featured.title}
                </h1>

                {featured.genres.length > 0 && (
                  <p className="text-sm text-muted">{featured.genres.join(' · ')}</p>
                )}

                <div className="flex flex-wrap items-center justify-center gap-x-5 gap-y-2 text-[13px] text-muted md:justify-start">
                  <span className="flex items-center gap-1.5"><Clock size={14} />{featured.durationMinutes} phút</span>
                  <span className="flex items-center gap-1.5">
                    <CalendarDays size={14} />Khởi chiếu {featured.releaseDate.split('-').reverse().join('/')}
                  </span>
                  <span title={AGE_RATING_LABEL[featured.ageRating]}
                        className="rounded-md border border-ink-600 bg-ink-900/60 px-2 py-0.5 font-medium backdrop-blur">
                    {featured.ageRating}
                  </span>
                </div>

                <div className="flex flex-wrap justify-center gap-3 md:justify-start">
                  <Link to={`/phim/${featured.id}`}>
                    <Button size="lg"><Ticket size={16} /> Đặt vé ngay</Button>
                  </Link>
                  {featured.trailerUrl && (
                    <Button size="lg" variant="secondary" onClick={() => setTrailer(featured)}>
                      <Play size={15} fill="currentColor" /> Xem trailer
                    </Button>
                  )}
                </div>

                {/* Chọn nhanh giữa các phim nổi bật */}
                {spotlight.length > 1 && (
                  <div className="mt-1 flex items-center justify-center gap-2 md:justify-start">
                    {spotlight.map((m, i) => (
                      <button key={m.id} onClick={() => setHeroIndex(i)}
                              aria-label={`Xem ${m.title}`} aria-current={i === heroIndex}
                              className={clsx(
                                'h-1.5 rounded-full transition-all duration-300',
                                i === heroIndex ? 'w-8 bg-brand-500' : 'w-4 bg-ink-600 hover:bg-ink-500',
                              )} />
                    ))}
                  </div>
                )}
              </div>
            </div>
          </>
        ) : (
          <div className="mx-auto grid max-w-6xl gap-8 px-4 py-16 md:grid-cols-[240px_1fr]">
            <Skeleton className="mx-auto aspect-[2/3] w-40 rounded-2xl md:mx-0 md:w-full" />
            <div className="flex flex-col justify-center gap-4">
              <Skeleton className="h-5 w-44" />
              <Skeleton className="h-12 w-3/4" />
              <Skeleton className="h-4 w-1/2" />
              <Skeleton className="h-11 w-40 rounded-xl" />
            </div>
          </div>
        )}
      </section>

      {/* ══════════════════ Dải số liệu ══════════════════ */}
      <section className="border-b border-ink-800 bg-ink-900/60">
        <div className="mx-auto grid max-w-6xl gap-6 px-4 py-7 sm:grid-cols-2 lg:grid-cols-4">
          <StatTile icon={<MapPin size={17} />} value={cinemas?.length ?? '—'} label="Rạp trên toàn quốc" />
          <StatTile icon={<CalendarDays size={17} />} value={todayShows?.length ?? '—'} label="Suất chiếu hôm nay" />
          <StatTile icon={<Timer size={17} />} value="8 phút" label="Thời gian giữ ghế cho bạn" />
          <StatTile icon={<QrCode size={17} />} value="Vé QR" label="Nhận vé ngay sau thanh toán" />
        </div>
      </section>

      {/* ══════════════════ Danh sách phim ══════════════════ */}
      <section className="mx-auto max-w-6xl px-4 py-14">
        <SectionHeading
          eyebrow="Lịch chiếu"
          title={tab === 'NowShowing' ? 'Phim đang chiếu' : 'Phim sắp chiếu'}
          description={tab === 'NowShowing'
            ? 'Chọn phim, chọn ghế và thanh toán trong vài phút.'
            : 'Những phim sắp ra rạp — đặt lịch để không bỏ lỡ suất đầu tiên.'}
          action={
            <div className="flex gap-2">
              {([['NowShowing', 'Đang chiếu'], ['ComingSoon', 'Sắp chiếu']] as const).map(([key, label]) => (
                <button key={key} onClick={() => setTab(key)}
                  className={clsx(
                    'rounded-full px-4 py-2 text-sm transition-colors',
                    tab === key
                      ? 'bg-brand-500 font-semibold text-on-brand'
                      : 'border border-ink-700 text-muted hover:border-ink-600 hover:text-cream',
                  )}>
                  {label}
                </button>
              ))}
            </div>
          }
        />

        {isLoading ? <PosterGridSkeleton /> : (
          <div className="stagger grid grid-cols-2 gap-x-5 gap-y-8 sm:grid-cols-3 lg:grid-cols-5">
            {list?.map((m) => <MovieCard key={m.id} movie={m} onTrailer={setTrailer} />)}
          </div>
        )}

        <div className="mt-9 flex justify-center">
          <Link to="/phim">
            <Button variant="secondary">Xem toàn bộ phim <ArrowRight size={15} /></Button>
          </Link>
        </div>
      </section>

      {/* ══════════════════ Suất chiếu hôm nay ══════════════════ */}
      {todayShows && todayShows.length > 0 && (
        <section className="border-y border-ink-800 bg-ink-900/60">
          <div className="mx-auto max-w-6xl px-4 py-14">
            <SectionHeading eyebrow="Hôm nay" title="Suất chiếu gần nhất"
                            description="Còn ghế trống, đặt là xem." />
            <div className="no-scrollbar -mx-4 flex gap-4 overflow-x-auto px-4 pb-2">
              {todayShows.slice(0, 12).map((s) => (
                <Link key={s.id} to={`/dat-ve/${s.id}`}
                      className="lift group flex w-64 shrink-0 flex-col gap-3 rounded-2xl border border-ink-700 bg-ink-850 p-4
                                 hover:border-brand-600/60">
                  <div className="flex items-start justify-between gap-3">
                    <span className="font-display text-2xl font-extrabold tabular text-brand-400">{time(s.startsAtUtc)}</span>
                    <Badge tone={s.roomType === 'Imax' ? 'brand' : 'outline'}>
                      {ROOM_TYPE_LABEL[s.roomType] ?? s.roomType}
                    </Badge>
                  </div>
                  <p className="line-clamp-2 text-sm font-semibold leading-snug transition-colors group-hover:text-brand-400">
                    {s.movieTitle}
                  </p>
                  <p className="flex items-center gap-1.5 text-[12px] text-faint">
                    <MapPin size={12} className="shrink-0" />
                    <span className="truncate">{s.cinemaName} · {s.roomName}</span>
                  </p>
                  <div className="mt-auto flex items-center justify-between border-t border-ink-700 pt-3 text-[12px]">
                    <span className="text-faint">{s.availableSeats}/{s.totalSeats} ghế trống</span>
                    <span className="font-semibold text-cream tabular">từ {money(s.fromPrice)}</span>
                  </div>
                </Link>
              ))}
            </div>
          </div>
        </section>
      )}

      {/* ══════════════════ Ưu đãi ══════════════════ */}
      {promos && promos.length > 0 && (
        <section className="mx-auto max-w-6xl px-4 py-14">
          <SectionHeading
            eyebrow="Ưu đãi" title="Mã giảm giá đang chạy"
            description="Nhập mã ở bước bắp nước khi đặt vé."
            action={<Link to="/uu-dai" className="text-[13px] text-brand-400 hover:underline">Xem tất cả</Link>}
          />
          <div className="grid gap-4 sm:grid-cols-2">
            {promos.slice(0, 2).map((p) => <PromoTicket key={p.id} promo={p} />)}
          </div>
        </section>
      )}

      {/* ══════════════════ Ba bước đặt vé ══════════════════ */}
      <section className="border-t border-ink-800 bg-ink-900/60">
        <div className="mx-auto max-w-6xl px-4 py-14">
          <SectionHeading eyebrow="Cách hoạt động" title="Đặt vé trong ba bước" />
          <ol className="grid gap-5 md:grid-cols-3">
            {[
              { icon: <MousePointerClick size={20} />, title: 'Chọn ghế', text: 'Sơ đồ ghế cập nhật liên tục, thấy ngay ghế người khác vừa giữ.' },
              { icon: <Timer size={20} />, title: 'Giữ ghế 8 phút', text: 'Ghế được khoá riêng cho bạn, đồng hồ đếm ngược hiện ngay trên màn hình.' },
              { icon: <QrCode size={20} />, title: 'Nhận vé QR', text: 'Thanh toán xong là có mã QR, đưa nhân viên quét ở cửa phòng chiếu.' },
            ].map((s, i) => (
              <li key={s.title} className="surface relative overflow-hidden p-6">
                <span aria-hidden
                      className="absolute -right-2 -top-4 font-display text-[76px] font-extrabold leading-none text-ink-800">
                  {i + 1}
                </span>
                <div className="relative">
                  <div className="mb-4 flex h-11 w-11 items-center justify-center rounded-xl bg-brand-950 text-brand-400">
                    {s.icon}
                  </div>
                  <p className="font-display text-lg font-bold">{s.title}</p>
                  <p className="mt-1.5 text-[13px] leading-relaxed text-faint">{s.text}</p>
                </div>
              </li>
            ))}
          </ol>
        </div>
      </section>

      <TrailerModal title={trailer?.title ?? ''} url={trailer?.trailerUrl ?? null}
                    onClose={() => setTrailer(null)} />
    </div>
  )
}

/** Thẻ ưu đãi cắt hình cuống vé, để mục này khác hẳn các thẻ vuông còn lại. */
function PromoTicket({ promo }: { promo: Promotion }) {
  return (
    <div className="lift surface ticket-edge flex items-stretch overflow-hidden">
      <div className="flex w-16 shrink-0 items-center justify-center bg-brand-950 text-brand-400">
        <Tag size={22} />
      </div>
      <div className="dashed-cut w-px shrink-0 bg-transparent" />
      <div className="min-w-0 flex-1 p-5">
        <p className="font-display text-lg font-bold">{promo.name}</p>
        <p className="mt-1 text-[13px] text-faint">
          {promo.discountType === 'Percentage'
            ? `Giảm ${promo.value}%${promo.maxDiscount ? `, tối đa ${money(promo.maxDiscount)}` : ''}`
            : `Giảm ${money(promo.value)}`}
          {promo.minOrderAmount > 0 && ` · đơn từ ${money(promo.minOrderAmount)}`}
        </p>
        <code className="mt-3 inline-block rounded-lg border border-dashed border-brand-600/50 bg-brand-950 px-3 py-1
                         font-mono text-[13px] font-bold tracking-wider text-brand-400">
          {promo.code}
        </code>
      </div>
    </div>
  )
}
