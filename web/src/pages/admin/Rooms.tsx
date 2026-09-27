import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { MapPin, Plus, Projector } from 'lucide-react'
import { api, ApiError } from '@/lib/api'
import type { Cinema, Room } from '@/lib/types'
import { ROOM_TYPE_LABEL } from '@/lib/format'
import { Badge, Button, Card, ErrorNote, Field, Input, Modal, Select, Spinner } from '@/components/ui'

export default function AdminRooms() {
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [cinemaId, setCinemaId] = useState('')

  const { data: cinemas, isLoading } = useQuery({
    queryKey: ['cinemas'], queryFn: () => api.get<Cinema[]>('/api/cinemas'),
  })

  if (isLoading) return <Spinner />

  return (
    <div className="p-6 lg:p-8">
      <div className="mb-6 flex flex-wrap items-center justify-between gap-4">
        <h1 className="text-2xl font-extrabold">Rạp & phòng chiếu</h1>
        <Button onClick={() => { setCinemaId(cinemas?.[0]?.id ?? ''); setOpen(true) }}>
          <Plus size={16} /> Thêm phòng chiếu
        </Button>
      </div>

      <div className="grid gap-4 md:grid-cols-2">
        {cinemas?.map((c) => <CinemaBlock key={c.id} cinema={c} />)}
      </div>

      <CreateRoomModal open={open} onClose={() => setOpen(false)}
                       cinemas={cinemas ?? []} cinemaId={cinemaId} setCinemaId={setCinemaId}
                       onCreated={() => qc.invalidateQueries({ queryKey: ['rooms'] })} />
    </div>
  )
}

function CinemaBlock({ cinema }: { cinema: Cinema }) {
  const { data: rooms } = useQuery({
    queryKey: ['rooms', cinema.id],
    queryFn: () => api.get<Room[]>(`/api/cinemas/${cinema.id}/rooms`),
  })

  return (
    <Card className="p-5">
      <h2 className="font-display text-lg font-bold">{cinema.name}</h2>
      <p className="mt-1.5 flex items-start gap-2 text-[13px] text-faint">
        <MapPin size={13} className="mt-0.5 shrink-0" /> {cinema.address}, {cinema.city}
      </p>

      <div className="mt-4 flex flex-col gap-2 border-t border-ink-700 pt-4">
        {!rooms?.length
          ? <p className="text-[13px] text-faint">Chưa có phòng chiếu nào.</p>
          : rooms.map((r) => (
            <div key={r.id} className="flex items-center justify-between rounded-lg bg-ink-900 px-3.5 py-2.5">
              <span className="flex items-center gap-2 text-sm">
                <Projector size={14} className="text-faint" /> {r.name}
              </span>
              <span className="flex items-center gap-2">
                <Badge tone={r.roomType === 'Imax' ? 'brand' : 'outline'}>
                  {ROOM_TYPE_LABEL[r.roomType] ?? r.roomType}
                </Badge>
                <span className="text-[12px] text-faint tabular">{r.seatCount} ghế</span>
              </span>
            </div>
          ))}
      </div>
    </Card>
  )
}

function CreateRoomModal({ open, onClose, cinemas, cinemaId, setCinemaId, onCreated }: {
  open: boolean; onClose: () => void; cinemas: Cinema[]
  cinemaId: string; setCinemaId: (v: string) => void; onCreated: () => void
}) {
  const [name, setName] = useState('')
  const [roomType, setRoomType] = useState('TwoD')
  const [rowCount, setRowCount] = useState(8)
  const [seatsPerRow, setSeatsPerRow] = useState(12)
  const [vipFromRow, setVipFromRow] = useState(4)
  const [vipToRow, setVipToRow] = useState(7)
  const [error, setError] = useState<string | null>(null)

  const create = useMutation({
    mutationFn: () => api.post('/api/rooms', {
      cinemaId, name, roomType, rowCount, seatsPerRow, vipFromRow, vipToRow,
    }),
    onSuccess: () => { setError(null); onCreated(); onClose(); setName('') },
    onError: (e: ApiError) => setError(e.message),
  })

  const total = rowCount * seatsPerRow

  return (
    <Modal open={open} onClose={onClose} title="Thêm phòng chiếu">
      <form onSubmit={(e) => { e.preventDefault(); create.mutate() }} className="flex flex-col gap-4">
        {error && <ErrorNote>{error}</ErrorNote>}

        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Rạp">
            <Select required value={cinemaId} onChange={(e) => setCinemaId(e.target.value)}>
              {cinemas.map((c) => <option key={c.id} value={c.id}>{c.name}</option>)}
            </Select>
          </Field>
          <Field label="Tên phòng">
            <Input required value={name} onChange={(e) => setName(e.target.value)} placeholder="Phòng 4" />
          </Field>
          <Field label="Loại phòng">
            <Select value={roomType} onChange={(e) => setRoomType(e.target.value)}>
              <option value="TwoD">2D</option>
              <option value="ThreeD">3D</option>
              <option value="Imax">IMAX</option>
            </Select>
          </Field>
          <Field label="Số hàng ghế" hint="Tối đa 26 hàng, đặt tên A đến Z">
            <Input type="number" min={1} max={26} value={rowCount}
                   onChange={(e) => setRowCount(Number(e.target.value))} />
          </Field>
          <Field label="Số ghế mỗi hàng">
            <Input type="number" min={1} max={40} value={seatsPerRow}
                   onChange={(e) => setSeatsPerRow(Number(e.target.value))} />
          </Field>
          <Field label="Hàng VIP từ / đến">
            <div className="flex gap-2">
              <Input type="number" min={0} value={vipFromRow}
                     onChange={(e) => setVipFromRow(Number(e.target.value))} />
              <Input type="number" min={0} value={vipToRow}
                     onChange={(e) => setVipToRow(Number(e.target.value))} />
            </div>
          </Field>
        </div>

        <div className="rounded-xl border border-ink-700 bg-ink-900 p-3.5 text-[13px] text-muted">
          Sẽ sinh <b className="text-cream">{total} ghế</b> — hàng{' '}
          <b className="text-cream">{String.fromCharCode(65)}</b> đến{' '}
          <b className="text-cream">{String.fromCharCode(64 + Math.max(1, rowCount))}</b>,
          hàng cuối là ghế đôi theo thông lệ rạp Việt Nam.
          <br />Giá vé ghế thường, VIP và ghế đôi đặt ở bước <b className="text-cream">tạo suất chiếu</b>,
          vì mỗi suất một giá tuỳ khung giờ và ngày chiếu.
        </div>

        <div className="flex justify-end gap-2 pt-1">
          <Button type="button" variant="ghost" onClick={onClose}>Hủy</Button>
          <Button type="submit" loading={create.isPending}>Tạo phòng</Button>
        </div>
      </form>
    </Modal>
  )
}
