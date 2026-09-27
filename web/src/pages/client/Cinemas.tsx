import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Building2, MapPin, Phone, Projector } from 'lucide-react'
import clsx from 'clsx'
import { api } from '@/lib/api'
import type { Cinema, Room } from '@/lib/types'
import { ROOM_TYPE_LABEL } from '@/lib/format'
import { Badge, EmptyState, SectionHeading, Skeleton } from '@/components/ui'

export default function Cinemas() {
  const [city, setCity] = useState('')

  const { data: cities } = useQuery({
    queryKey: ['cities'], queryFn: () => api.get<string[]>('/api/cinemas/cities'),
  })
  const { data, isLoading } = useQuery({
    queryKey: ['cinemas', city],
    queryFn: () => api.get<Cinema[]>(`/api/cinemas${city ? `?city=${encodeURIComponent(city)}` : ''}`),
  })

  return (
    <div className="mx-auto max-w-5xl px-4 py-10">
      <SectionHeading eyebrow="Hệ thống" title="Rạp CineBook"
                      description="Chọn thành phố để xem các cụm rạp và phòng chiếu." />

      <div className="mb-7 flex flex-wrap gap-2">
        <CityChip active={city === ''} onClick={() => setCity('')}>Toàn quốc</CityChip>
        {cities?.map((c) => (
          <CityChip key={c} active={city === c} onClick={() => setCity(c)}>{c}</CityChip>
        ))}
      </div>

      {isLoading ? (
        <div className="grid gap-4 md:grid-cols-2">
          {[0, 1].map((i) => <Skeleton key={i} className="h-56 rounded-[14px]" />)}
        </div>
      ) : !data?.length ? (
        <EmptyState icon={<Building2 size={38} />} title="Chưa có rạp ở khu vực này" />
      ) : (
        <div className="stagger grid gap-4 md:grid-cols-2">
          {data.map((c) => <CinemaCard key={c.id} cinema={c} />)}
        </div>
      )}
    </div>
  )
}

function CinemaCard({ cinema }: { cinema: Cinema }) {
  const { data: rooms } = useQuery({
    queryKey: ['rooms', cinema.id],
    queryFn: () => api.get<Room[]>(`/api/cinemas/${cinema.id}/rooms`),
  })

  const totalSeats = rooms?.reduce((sum, r) => sum + r.seatCount, 0) ?? 0

  return (
    <div className="lift surface flex flex-col overflow-hidden">
      {/* Dải đầu thẻ tạo điểm nhấn thay vì thẻ trắng trơn */}
      <div className="relative flex items-start gap-3 border-b border-ink-700 p-5">
        <div className="absolute inset-0 -z-10 screen-glow opacity-70" />
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-brand-950 text-brand-400">
          <Building2 size={19} />
        </div>
        <div className="min-w-0">
          <h3 className="font-display text-lg font-bold leading-snug">{cinema.name}</h3>
          <p className="mt-1 text-[12px] text-faint">
            {cinema.district ? `${cinema.district}, ` : ''}{cinema.city}
          </p>
        </div>
      </div>

      <div className="flex flex-1 flex-col gap-3 p-5">
        <p className="flex items-start gap-2 text-[13px] text-muted">
          <MapPin size={14} className="mt-0.5 shrink-0 text-faint" />
          {cinema.address}
        </p>
        {cinema.hotline && (
          <p className="flex items-center gap-2 text-[13px] text-muted">
            <Phone size={14} className="shrink-0 text-faint" /> {cinema.hotline}
          </p>
        )}

        {rooms && rooms.length > 0 && (
          <div className="mt-auto border-t border-ink-700 pt-4">
            <p className="mb-2.5 flex items-center gap-1.5 text-[12px] font-semibold text-muted">
              <Projector size={13} /> {rooms.length} phòng chiếu · {totalSeats} ghế
            </p>
            <div className="flex flex-wrap gap-2">
              {rooms.map((r) => (
                <Badge key={r.id} tone={r.roomType === 'Imax' ? 'brand' : 'outline'}>
                  {r.name} · {ROOM_TYPE_LABEL[r.roomType] ?? r.roomType} · {r.seatCount} ghế
                </Badge>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

function CityChip({ active, onClick, children }: {
  active: boolean; onClick: () => void; children: React.ReactNode
}) {
  return (
    <button onClick={onClick} className={clsx(
      'rounded-full px-4 py-2 text-[13px] transition-colors',
      active ? 'bg-brand-500 font-semibold text-on-brand'
             : 'border border-ink-700 text-muted hover:border-ink-600 hover:bg-ink-850 hover:text-cream',
    )}>{children}</button>
  )
}
