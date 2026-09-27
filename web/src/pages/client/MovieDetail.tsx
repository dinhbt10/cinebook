import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import {
  CalendarDays, Clock, Globe, MapPin, Play, Ticket, Users2, User2,
} from 'lucide-react'
import clsx from 'clsx'
import { api } from '@/lib/api'
import type { MovieDetail as Movie, Showtime } from '@/lib/types'
import {
  AGE_RATING_LABEL, isoDateVN, money, ROOM_TYPE_LABEL, SUBTITLE_LABEL, time, weekdayShort,
} from '@/lib/format'
import { Backdrop, Poster } from '@/components/Poster'
import { TrailerModal } from '@/components/TrailerModal'
import { Badge, Button, EmptyState, SectionHeading, Skeleton, Spinner } from '@/components/ui'

/** Bảy ngày tới, tính theo giờ Việt Nam. */
function nextDays(count = 7) {
  const today = new Date()
  return Array.from({ length: count }, (_, i) => {
    const d = new Date(today)
    d.setDate(d.getDate() + i)
    return d
  })
}

export default function MovieDetail() {
  const { id = '' } = useParams()
  const days = useMemo(() => nextDays(), [])
  const [date, setDate] = useState(() => isoDateVN(new Date()))
  const [trailerOpen, setTrailerOpen] = useState(false)

  const { data: movie, isLoading } = useQuery({
    queryKey: ['movie', id], queryFn: () => api.get<Movie>(`/api/movies/${id}`),
  })
  const { data: showtimes, isLoading: loadingShowtimes } = useQuery({
    queryKey: ['showtimes', id, date],
    queryFn: () => api.get<Showtime[]>(`/api/showtimes?movieId=${id}&date=${date}`),
  })

  const byCinema = useMemo(() => {
    const map = new Map<string, Showtime[]>()
    for (const s of showtimes ?? []) {
      const list = map.get(s.cinemaName) ?? []
      list.push(s)
      map.set(s.cinemaName, list)
    }
    return [...map.entries()]
  }, [showtimes])

  if (isLoading) return <Spinner label="Đang tải phim…" />
  if (!movie) return <div className="mx-auto max-w-6xl px-4 py-16"><EmptyState title="Không tìm thấy phim" /></div>

  return (
    <div>
      {/* ══════════════════ Đầu trang có ảnh nền ══════════════════ */}
      <section className="on-dark relative isolate overflow-hidden border-b border-ink-800">
        <div className="absolute inset-0 -z-20">
          <Backdrop title={movie.title} backdropUrl={movie.backdropUrl} />
        </div>
        <div className="absolute inset-0 -z-10 scrim-b" />
        <div className="absolute inset-0 -z-10 hidden scrim-l md:block" />
        <div className="absolute inset-0 -z-10 grain opacity-30" />

        <div className="relative mx-auto grid max-w-6xl gap-8 px-4 py-12 md:grid-cols-[220px_1fr] md:py-16">
          <div className="group relative mx-auto w-40 overflow-hidden rounded-2xl border border-ink-700/80 shadow-2xl shadow-ink-950 md:mx-0 md:w-full">
            <div className="aspect-[2/3] w-full">
              <Poster title={movie.title} posterUrl={movie.posterUrl} eager />
            </div>
            {movie.trailerUrl && (
              <button onClick={() => setTrailerOpen(true)}
                      className="absolute inset-0 flex items-center justify-center bg-ink-950/55 opacity-0
                                 transition-opacity duration-300 group-hover:opacity-100"
                      aria-label="Xem trailer">
                <span className="flex h-14 w-14 items-center justify-center rounded-full bg-brand-500 text-on-brand">
                  <Play size={20} fill="currentColor" />
                </span>
              </button>
            )}
          </div>

          <div className="flex flex-col gap-4">
            <div>
              <h1 className="font-display text-3xl font-extrabold leading-tight md:text-5xl">{movie.title}</h1>
              {movie.originalTitle && <p className="mt-1.5 text-sm text-muted">{movie.originalTitle}</p>}
            </div>

            <div className="flex flex-wrap gap-2">
              <Badge tone="brand">{movie.ageRating}</Badge>
              {movie.genres.map((g) => <Badge key={g}>{g}</Badge>)}
            </div>

            <p className="text-[12.5px] text-faint">{AGE_RATING_LABEL[movie.ageRating]}</p>

            <dl className="grid gap-x-8 gap-y-2.5 text-[13px] sm:grid-cols-2">
              <Meta icon={<Clock size={14} />} label="Thời lượng" value={`${movie.durationMinutes} phút`} />
              <Meta icon={<CalendarDays size={14} />} label="Khởi chiếu"
                    value={movie.releaseDate.split('-').reverse().join('/')} />
              {movie.director && <Meta icon={<User2 size={14} />} label="Đạo diễn" value={movie.director} />}
              {movie.language && <Meta icon={<Globe size={14} />} label="Ngôn ngữ" value={movie.language} />}
              {movie.cast && <Meta icon={<Users2 size={14} />} label="Diễn viên" value={movie.cast} />}
            </dl>

            {movie.description && (
              <p className="max-w-2xl text-sm leading-relaxed text-muted">{movie.description}</p>
            )}

            <div className="mt-1 flex flex-wrap gap-3">
              <a href="#lich-chieu">
                <Button size="lg"><Ticket size={16} /> Chọn suất chiếu</Button>
              </a>
              {movie.trailerUrl && (
                <Button size="lg" variant="secondary" onClick={() => setTrailerOpen(true)}>
                  <Play size={15} fill="currentColor" /> Trailer
                </Button>
              )}
            </div>
          </div>
        </div>
      </section>

      {/* ══════════════════ Lịch chiếu ══════════════════ */}
      <section id="lich-chieu" className="mx-auto max-w-6xl scroll-mt-20 px-4 py-12">
        <SectionHeading eyebrow="Đặt vé" title="Lịch chiếu"
                        description="Chọn ngày rồi bấm vào giờ chiếu để tới sơ đồ ghế." />

        <div className="no-scrollbar mb-8 flex gap-2 overflow-x-auto pb-1">
          {days.map((d, i) => {
            const value = isoDateVN(d)
            const active = value === date
            return (
              <button key={value} onClick={() => setDate(value)}
                className={clsx(
                  'flex min-w-17 shrink-0 flex-col items-center gap-0.5 rounded-xl border px-3.5 py-2.5 transition-all',
                  active
                    ? 'border-brand-500 bg-brand-500 text-on-brand shadow-[0_8px_24px_-10px_rgba(229, 9, 20,.7)]'
                    : 'border-ink-700 text-muted hover:border-ink-600 hover:bg-ink-850 hover:text-cream',
                )}>
                <span className="text-[11px] font-medium opacity-80">
                  {i === 0 ? 'Hôm nay' : weekdayShort(d)}
                </span>
                <span className="text-lg font-bold leading-none tabular">{d.getDate()}</span>
                <span className="text-[10px] opacity-70">th{d.getMonth() + 1}</span>
              </button>
            )
          })}
        </div>

        {loadingShowtimes ? (
          <div className="flex flex-col gap-4">
            {[0, 1].map((i) => (
              <div key={i} className="surface flex flex-col gap-4 p-5">
                <Skeleton className="h-5 w-52" />
                <div className="flex gap-2.5">
                  {[0, 1, 2, 3].map((j) => <Skeleton key={j} className="h-20 w-36 rounded-xl" />)}
                </div>
              </div>
            ))}
          </div>
        ) : byCinema.length === 0 ? (
          <EmptyState icon={<MapPin size={36} />} title="Chưa có suất chiếu cho ngày này"
                      description="Hãy thử chọn một ngày khác trong tuần." />
        ) : (
          <div className="flex flex-col gap-4">
            {byCinema.map(([cinema, list]) => (
              <div key={cinema} className="surface overflow-hidden">
                <div className="flex items-center gap-2.5 border-b border-ink-700 bg-ink-900/60 px-5 py-3.5">
                  <MapPin size={16} className="text-brand-500" />
                  <h3 className="text-base font-bold">{cinema}</h3>
                  <span className="ml-auto text-[12px] text-faint">{list.length} suất</span>
                </div>
                <div className="flex flex-wrap gap-2.5 p-5">
                  {list.map((s) => {
                    const almostFull = s.availableSeats <= s.totalSeats * 0.15
                    return (
                      <Link key={s.id} to={`/dat-ve/${s.id}`}
                        className="lift group flex min-w-36 flex-col gap-1 rounded-xl border border-ink-600 bg-ink-900 px-4 py-3
                                   hover:border-brand-500">
                        <span className="font-display text-xl font-bold leading-none tabular transition-colors group-hover:text-brand-400">
                          {time(s.startsAtUtc)}
                        </span>
                        <span className="text-[11px] text-faint">
                          {s.roomName} · {ROOM_TYPE_LABEL[s.roomType] ?? s.roomType} · {SUBTITLE_LABEL[s.subtitleMode]}
                        </span>
                        <span className={clsx('text-[11px]', almostFull ? 'font-medium text-velvet-400' : 'text-muted')}>
                          {almostFull ? `Sắp hết · còn ${s.availableSeats} ghế` : `${s.availableSeats} ghế trống`}
                        </span>
                        <span className="mt-0.5 text-[11px] font-semibold text-cream tabular">
                          từ {money(s.fromPrice)}
                        </span>
                      </Link>
                    )
                  })}
                </div>
              </div>
            ))}
          </div>
        )}
      </section>

      <TrailerModal title={movie.title} url={trailerOpen ? movie.trailerUrl : null}
                    onClose={() => setTrailerOpen(false)} />
    </div>
  )
}

function Meta({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return (
    <div className="flex items-start gap-2">
      <span className="mt-0.5 shrink-0 text-faint">{icon}</span>
      <dt className="shrink-0 text-faint">{label}:</dt>
      <dd className="min-w-0 font-medium">{value}</dd>
    </div>
  )
}
