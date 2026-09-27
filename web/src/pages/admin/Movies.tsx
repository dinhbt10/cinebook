import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { api, ApiError } from '@/lib/api'
import type { MovieDetail, MovieListItem } from '@/lib/types'
import { Badge, Button, Card, ErrorNote, Field, Input, Modal, Select, Spinner, Textarea } from '@/components/ui'

const AGE_RATINGS = ['P', 'K', 'T13', 'T16', 'T18']
const STATUSES = [
  ['Draft', 'Bản nháp'], ['ComingSoon', 'Sắp chiếu'],
  ['NowShowing', 'Đang chiếu'], ['Ended', 'Đã kết thúc'],
]

const EMPTY = {
  title: '', originalTitle: '', description: '', durationMinutes: 100,
  releaseDate: new Date().toISOString().slice(0, 10), endDate: '',
  ageRating: 'T13', director: '', cast: '', language: 'Tiếng Việt', country: 'Việt Nam',
  posterUrl: '', backdropUrl: '', trailerUrl: '', status: 'NowShowing',
}
type Form = typeof EMPTY

export default function AdminMovies() {
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<string | null>(null)
  const [form, setForm] = useState<Form>(EMPTY)
  const [error, setError] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'movies'], queryFn: () => api.get<MovieListItem[]>('/api/movies'),
  })

  const save = useMutation({
    mutationFn: async () => {
      const body = {
        ...form,
        originalTitle: form.originalTitle || null,
        description: form.description || null,
        endDate: form.endDate || null,
        director: form.director || null, cast: form.cast || null,
        posterUrl: form.posterUrl || null, backdropUrl: form.backdropUrl || null,
        trailerUrl: form.trailerUrl || null,
        genreIds: [],
      }
      return editing ? api.put(`/api/movies/${editing}`, body) : api.post('/api/movies', body)
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['admin', 'movies'] })
      setOpen(false); setError(null)
    },
    onError: (e: ApiError) => setError(e.message),
  })

  const remove = useMutation({
    mutationFn: (id: string) => api.del(`/api/movies/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['admin', 'movies'] }),
    onError: (e: ApiError) => setError(e.message),
  })

  const openCreate = () => { setEditing(null); setForm(EMPTY); setError(null); setOpen(true) }

  const openEdit = async (id: string) => {
    const m = await api.get<MovieDetail>(`/api/movies/${id}`)
    setEditing(id); setError(null)
    setForm({
      title: m.title, originalTitle: m.originalTitle ?? '', description: m.description ?? '',
      durationMinutes: m.durationMinutes, releaseDate: m.releaseDate, endDate: '',
      ageRating: m.ageRating, director: m.director ?? '', cast: m.cast ?? '',
      language: m.language ?? '', country: '', posterUrl: m.posterUrl ?? '',
      backdropUrl: m.backdropUrl ?? '', trailerUrl: m.trailerUrl ?? '', status: m.status,
    })
    setOpen(true)
  }

  const set = <K extends keyof Form>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement | HTMLSelectElement>,
  ) => setForm((s) => ({ ...s, [k]: k === 'durationMinutes' ? Number(e.target.value) : e.target.value }))

  return (
    <div className="p-6 lg:p-8">
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="text-2xl font-extrabold">Quản lý phim</h1>
        <Button onClick={openCreate}><Plus size={16} /> Thêm phim</Button>
      </div>

      {error && <div className="mb-4"><ErrorNote>{error}</ErrorNote></div>}

      {isLoading ? <Spinner /> : (
        <Card className="overflow-x-auto">
          <table className="w-full min-w-[680px] text-sm">
            <thead>
              <tr className="border-b border-ink-700 text-left text-[11px] uppercase tracking-wide text-faint">
                <th className="px-5 py-3.5 font-semibold">Phim</th>
                <th className="px-5 py-3.5 font-semibold">Thời lượng</th>
                <th className="px-5 py-3.5 font-semibold">Nhãn tuổi</th>
                <th className="px-5 py-3.5 font-semibold">Trạng thái</th>
                <th className="px-5 py-3.5" />
              </tr>
            </thead>
            <tbody>
              {data?.map((m) => (
                <tr key={m.id} className="border-b border-ink-800 last:border-0">
                  <td className="px-5 py-3">
                    <div className="flex items-center gap-3">
                      {m.posterUrl && <img src={m.posterUrl} alt="" className="h-12 w-8 rounded object-cover" />}
                      <div className="min-w-0">
                        <p className="truncate font-medium">{m.title}</p>
                        <p className="text-[12px] text-faint">{m.genres.join(', ') || '—'}</p>
                      </div>
                    </div>
                  </td>
                  <td className="px-5 py-3 tabular">{m.durationMinutes} phút</td>
                  <td className="px-5 py-3"><Badge>{m.ageRating}</Badge></td>
                  <td className="px-5 py-3">
                    <Badge tone={m.status === 'NowShowing' ? 'mint' : m.status === 'ComingSoon' ? 'brand' : 'faint'}>
                      {STATUSES.find(([k]) => k === m.status)?.[1] ?? m.status}
                    </Badge>
                  </td>
                  <td className="px-5 py-3">
                    <div className="flex justify-end gap-1">
                      <button onClick={() => openEdit(m.id)} title="Sửa"
                              className="rounded-lg p-2 text-faint transition-colors hover:bg-ink-800 hover:text-cream">
                        <Pencil size={15} />
                      </button>
                      <button onClick={() => remove.mutate(m.id)} title="Xóa"
                              className="rounded-lg p-2 text-faint transition-colors hover:bg-ink-800 hover:text-velvet-400">
                        <Trash2 size={15} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </Card>
      )}

      <Modal open={open} onClose={() => setOpen(false)} wide
             title={editing ? 'Sửa thông tin phim' : 'Thêm phim mới'}>
        <form onSubmit={(e) => { e.preventDefault(); save.mutate() }} className="flex flex-col gap-4">
          {error && <ErrorNote>{error}</ErrorNote>}
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Tên phim"><Input required value={form.title} onChange={set('title')} /></Field>
            <Field label="Tên gốc"><Input value={form.originalTitle} onChange={set('originalTitle')} /></Field>
            <Field label="Thời lượng (phút)">
              <Input type="number" required min={1} value={form.durationMinutes} onChange={set('durationMinutes')} />
            </Field>
            <Field label="Ngày khởi chiếu">
              <Input type="date" required value={form.releaseDate} onChange={set('releaseDate')} />
            </Field>
            <Field label="Nhãn phân loại tuổi">
              <Select value={form.ageRating} onChange={set('ageRating')}>
                {AGE_RATINGS.map((r) => <option key={r} value={r}>{r}</option>)}
              </Select>
            </Field>
            <Field label="Trạng thái">
              <Select value={form.status} onChange={set('status')}>
                {STATUSES.map(([k, v]) => <option key={k} value={k}>{v}</option>)}
              </Select>
            </Field>
            <Field label="Đạo diễn"><Input value={form.director} onChange={set('director')} /></Field>
            <Field label="Ngôn ngữ"><Input value={form.language} onChange={set('language')} /></Field>
          </div>
          <Field label="Ảnh poster" hint="Dán đường dẫn ảnh">
            <Input value={form.posterUrl} onChange={set('posterUrl')} placeholder="https://…" />
          </Field>
          <Field label="Mô tả"><Textarea value={form.description} onChange={set('description')} /></Field>

          <div className="flex justify-end gap-2 pt-1">
            <Button type="button" variant="ghost" onClick={() => setOpen(false)}>Hủy</Button>
            <Button type="submit" loading={save.isPending}>{editing ? 'Lưu thay đổi' : 'Thêm phim'}</Button>
          </div>
        </form>
      </Modal>
    </div>
  )
}
