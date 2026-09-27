import { Link } from 'react-router-dom'
import { Clock, Play, Ticket } from 'lucide-react'
import clsx from 'clsx'
import { Badge } from '@/components/ui'
import { Poster } from '@/components/Poster'
import type { MovieListItem } from '@/lib/types'

const RATING_TONE: Record<string, 'mint' | 'brand' | 'velvet'> = {
  P: 'mint', K: 'mint', T13: 'brand', T16: 'brand', T18: 'velvet',
}

export function MovieCard({ movie, onTrailer }: {
  movie: MovieListItem
  onTrailer?: (movie: MovieListItem) => void
}) {
  const coming = movie.status === 'ComingSoon'

  return (
    <div className="group flex flex-col gap-3">
      <div className="lift relative aspect-[2/3] overflow-hidden rounded-xl border border-ink-700 bg-ink-800">
        <Link to={`/phim/${movie.id}`} className="absolute inset-0" aria-label={movie.title}>
          <div className="h-full w-full transition-transform duration-700 group-hover:scale-[1.07]">
            <Poster title={movie.title} posterUrl={movie.posterUrl} />
          </div>
        </Link>

        {/* Lớp phủ chỉ hiện khi rê chuột, giữ cho lưới lúc bình thường vẫn thoáng */}
        <div className="pointer-events-none absolute inset-0 flex flex-col justify-end
                        bg-gradient-to-t from-ink-950 via-ink-950/55 to-transparent p-3.5
                        opacity-0 transition-opacity duration-300 group-hover:opacity-100">
          {movie.genres.length > 0 && (
            <p className="mb-2 line-clamp-1 text-[11px] text-muted">{movie.genres.join(' · ')}</p>
          )}
          <div className="pointer-events-auto flex items-center gap-2">
            <Link to={`/phim/${movie.id}`}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-brand-500 px-3 py-1.5 text-[12px] font-semibold text-on-brand
                             transition-colors hover:bg-brand-400">
              <Ticket size={12} /> {coming ? 'Chi tiết' : 'Đặt vé'}
            </Link>
            {movie.trailerUrl && onTrailer && (
              <button onClick={() => onTrailer(movie)} title="Xem trailer"
                      className="inline-flex h-8 w-8 items-center justify-center rounded-lg border border-ink-600
                                 bg-ink-900/85 text-cream backdrop-blur transition-colors hover:border-brand-500 hover:text-brand-400">
                <Play size={12} fill="currentColor" />
              </button>
            )}
          </div>
        </div>

        <div className="pointer-events-none absolute left-2.5 top-2.5 flex flex-col items-start gap-1.5">
          <Badge tone={RATING_TONE[movie.ageRating] ?? 'outline'}>{movie.ageRating}</Badge>
          {coming && <Badge tone="outline" className="backdrop-blur">Sắp chiếu</Badge>}
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <Link to={`/phim/${movie.id}`}
              className="line-clamp-2 text-[15px] font-semibold leading-snug transition-colors group-hover:text-brand-400">
          {movie.title}
        </Link>
        <div className={clsx('flex items-center gap-2.5 text-[12px] text-faint')}>
          <span className="flex items-center gap-1"><Clock size={12} />{movie.durationMinutes} phút</span>
          {movie.genres.length > 0 && <span className="truncate">· {movie.genres.join(', ')}</span>}
        </div>
      </div>
    </div>
  )
}
