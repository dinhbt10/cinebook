import { useMemo, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Film, Search, X } from 'lucide-react'
import clsx from 'clsx'
import { api } from '@/lib/api'
import type { MovieListItem } from '@/lib/types'
import { MovieCard } from '@/components/MovieCard'
import { TrailerModal } from '@/components/TrailerModal'
import { EmptyState, Input, PosterGridSkeleton, SectionHeading } from '@/components/ui'

interface Genre { id: string; name: string; slug: string }

export default function Movies() {
  const [q, setQ] = useState('')
  const [status, setStatus] = useState<'NowShowing' | 'ComingSoon' | ''>('')
  const [genreId, setGenreId] = useState('')
  const [trailer, setTrailer] = useState<MovieListItem | null>(null)

  const { data: genres } = useQuery({
    queryKey: ['genres'], queryFn: () => api.get<Genre[]>('/api/genres'),
  })

  const query = useMemo(() => {
    const p = new URLSearchParams()
    if (q.trim()) p.set('q', q.trim())
    if (status) p.set('status', status)
    if (genreId) p.set('genreId', genreId)
    return p.toString()
  }, [q, status, genreId])

  const { data, isLoading } = useQuery({
    queryKey: ['movies', 'search', query],
    queryFn: () => api.get<MovieListItem[]>(`/api/movies?${query}`),
  })

  const hasFilter = !!(q || status || genreId)

  return (
    <div className="mx-auto max-w-6xl px-4 py-10">
      <SectionHeading
        eyebrow="Thư viện" title="Danh sách phim"
        description="Tìm theo tên, lọc theo trạng thái chiếu và thể loại."
        action={data && !isLoading
          ? <span className="text-[13px] text-faint">{data.length} phim</span>
          : undefined}
      />

      <div className="mb-8 flex flex-col gap-4">
        <div className="relative max-w-md">
          <Search size={16} className="pointer-events-none absolute left-3.5 top-1/2 -translate-y-1/2 text-faint" />
          <Input value={q} onChange={(e) => setQ(e.target.value)}
                 placeholder="Tìm theo tên phim…" className="pl-10 pr-10" />
          {q && (
            <button onClick={() => setQ('')} aria-label="Xoá từ khoá"
                    className="absolute right-3 top-1/2 -translate-y-1/2 rounded-md p-1 text-faint transition-colors hover:text-cream">
              <X size={14} />
            </button>
          )}
        </div>

        <div className="flex flex-wrap gap-2">
          <Chip active={status === ''} onClick={() => setStatus('')}>Tất cả</Chip>
          <Chip active={status === 'NowShowing'} onClick={() => setStatus('NowShowing')}>Đang chiếu</Chip>
          <Chip active={status === 'ComingSoon'} onClick={() => setStatus('ComingSoon')}>Sắp chiếu</Chip>
          <span className="mx-1 w-px self-stretch bg-ink-700" />
          <Chip active={genreId === ''} onClick={() => setGenreId('')}>Mọi thể loại</Chip>
          {genres?.map((g) => (
            <Chip key={g.id} active={genreId === g.id} onClick={() => setGenreId(g.id)}>{g.name}</Chip>
          ))}
        </div>
      </div>

      {isLoading ? <PosterGridSkeleton count={10} />
        : !data?.length
          ? <EmptyState icon={<Film size={40} />} title="Không tìm thấy phim nào"
                        description={hasFilter
                          ? 'Thử đổi từ khóa hoặc bỏ bớt bộ lọc.'
                          : 'Hiện chưa có phim nào được mở bán.'} />
          : (
            <div className="stagger grid grid-cols-2 gap-x-5 gap-y-8 sm:grid-cols-3 lg:grid-cols-5">
              {data.map((m) => <MovieCard key={m.id} movie={m} onTrailer={setTrailer} />)}
            </div>
          )}

      <TrailerModal title={trailer?.title ?? ''} url={trailer?.trailerUrl ?? null}
                    onClose={() => setTrailer(null)} />
    </div>
  )
}

function Chip({ active, onClick, children }: {
  active: boolean; onClick: () => void; children: React.ReactNode
}) {
  return (
    <button onClick={onClick} className={clsx(
      'rounded-full px-3.5 py-1.5 text-[13px] transition-colors',
      active ? 'bg-brand-500 font-semibold text-on-brand'
             : 'border border-ink-700 text-muted hover:border-ink-600 hover:bg-ink-850 hover:text-cream',
    )}>{children}</button>
  )
}
