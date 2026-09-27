import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Pencil, Plus, Power, Trash2 } from 'lucide-react'
import { api, ApiError } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import type { AdminPromotion } from '@/lib/types'
import { fullDateTime, money, utcFromVnInput, vnInputFromUtc } from '@/lib/format'
import { Badge, Button, Card, ErrorNote, Field, Input, Modal, Select, Spinner } from '@/components/ui'

// Ô số giữ dạng chuỗi để để trống được (trống = không giới hạn), đổi sang số lúc gửi.
const EMPTY = {
  code: '', name: '', discountType: 'Percentage', value: '10', maxDiscount: '',
  minOrderAmount: '0', startsLocal: '', endsLocal: '', usageLimit: '', perUserLimit: '1',
  isActive: true,
}
type Form = typeof EMPTY

const DAY_MS = 24 * 60 * 60 * 1000

function statusOf(p: AdminPromotion) {
  const now = Date.now()
  if (!p.isActive) return { label: 'Đã tắt', tone: 'faint' as const }
  if (now < new Date(p.startsAtUtc).getTime()) return { label: 'Chưa bắt đầu', tone: 'brand' as const }
  if (now > new Date(p.endsAtUtc).getTime()) return { label: 'Hết hạn', tone: 'velvet' as const }
  if (p.usageLimit !== null && p.usedCount >= p.usageLimit) return { label: 'Hết lượt', tone: 'velvet' as const }
  return { label: 'Đang chạy', tone: 'mint' as const }
}

function discountText(p: AdminPromotion) {
  if (p.discountType === 'FixedAmount') return money(p.value)
  return p.maxDiscount ? `${p.value}%, tối đa ${money(p.maxDiscount)}` : `${p.value}%`
}

export default function AdminPromotions() {
  const { hasRole } = useAuth()
  const isAdmin = hasRole('Admin')
  const qc = useQueryClient()
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<AdminPromotion | null>(null)
  const [form, setForm] = useState<Form>(EMPTY)
  const [error, setError] = useState<string | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  const { data, isLoading } = useQuery({
    queryKey: ['admin', 'promotions'],
    queryFn: () => api.get<AdminPromotion[]>('/api/promotions/all'),
    enabled: isAdmin,
  })

  const refresh = () => {
    qc.invalidateQueries({ queryKey: ['admin', 'promotions'] })
    // Trang chủ và trang Ưu đãi cũng hiện voucher, làm mới luôn cho khớp.
    qc.invalidateQueries({ queryKey: ['promotions'] })
    qc.invalidateQueries({ queryKey: ['vouchers'] })
  }

  const save = useMutation({
    mutationFn: async () => {
      const body = {
        code: form.code.trim().toUpperCase(),
        name: form.name.trim(),
        discountType: form.discountType,
        value: Number(form.value),
        maxDiscount: form.discountType === 'Percentage' && form.maxDiscount !== ''
          ? Number(form.maxDiscount) : null,
        minOrderAmount: Number(form.minOrderAmount || 0),
        startsAtUtc: utcFromVnInput(form.startsLocal),
        endsAtUtc: utcFromVnInput(form.endsLocal),
        usageLimit: form.usageLimit === '' ? null : Number(form.usageLimit),
        perUserLimit: Number(form.perUserLimit),
        isActive: form.isActive,
      }
      return editing
        ? api.put(`/api/promotions/${editing.id}`, body)
        : api.post('/api/promotions', body)
    },
    onSuccess: () => { refresh(); close() },
    onError: (e: ApiError) => setError(e.message),
  })

  const toggle = useMutation({
    mutationFn: (p: AdminPromotion) => api.patch(`/api/promotions/${p.id}/active?value=${!p.isActive}`),
    onSuccess: refresh,
    onError: (e: ApiError) => setError(e.message),
  })

  const remove = useMutation({
    mutationFn: (id: string) => api.del<{ message?: string } | undefined>(`/api/promotions/${id}`),
    // Voucher đã có đơn dùng thì backend chỉ tắt và trả lời kèm lý do.
    onSuccess: (res) => { setNotice(res?.message ?? null); refresh() },
    onError: (e: ApiError) => setError(e.message),
  })

  const openCreate = () => {
    setEditing(null); setError(null)
    setForm({
      ...EMPTY,
      startsLocal: vnInputFromUtc(new Date()),
      endsLocal: vnInputFromUtc(new Date(Date.now() + 30 * DAY_MS)),
    })
    setOpen(true)
  }

  const openEdit = (p: AdminPromotion) => {
    setEditing(p); setError(null)
    setForm({
      code: p.code, name: p.name, discountType: p.discountType, value: String(p.value),
      maxDiscount: p.maxDiscount === null ? '' : String(p.maxDiscount),
      minOrderAmount: String(p.minOrderAmount),
      startsLocal: vnInputFromUtc(p.startsAtUtc), endsLocal: vnInputFromUtc(p.endsAtUtc),
      usageLimit: p.usageLimit === null ? '' : String(p.usageLimit),
      perUserLimit: String(p.perUserLimit), isActive: p.isActive,
    })
    setOpen(true)
  }

  const confirmRemove = (p: AdminPromotion) => {
    const question = p.locked
      ? `Voucher ${p.code} đã có đơn sử dụng nên sẽ chỉ bị tắt, không xoá hẳn. Tiếp tục?`
      : `Xoá hẳn voucher ${p.code}? Không khôi phục được.`
    if (window.confirm(question)) { setNotice(null); remove.mutate(p.id) }
  }

  const set = <K extends Exclude<keyof Form, 'isActive'>>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>,
  ) => setForm((s) => ({ ...s, [k]: k === 'code' ? e.target.value.toUpperCase() : e.target.value }))

  const close = () => { setOpen(false); setError(null) }

  const locked = editing?.locked ?? false
  const isPercent = form.discountType === 'Percentage'

  if (!isAdmin) {
    return (
      <div className="p-6 lg:p-8">
        <ErrorNote>Chỉ quản trị viên hệ thống được quản lý khuyến mãi.</ErrorNote>
      </div>
    )
  }

  return (
    <div className="p-6 lg:p-8">
      <div className="mb-6 flex items-center justify-between gap-4">
        <h1 className="text-2xl font-extrabold">Quản lý khuyến mãi</h1>
        <Button onClick={openCreate}><Plus size={16} /> Tạo voucher</Button>
      </div>

      {error && !open && <div className="mb-4"><ErrorNote>{error}</ErrorNote></div>}
      {notice && (
        <div className="mb-4 rounded-lg border border-mint-500/35 bg-mint-950 px-4 py-3 text-sm text-mint-400">
          {notice}
        </div>
      )}

      {isLoading ? <Spinner /> : !data?.length ? (
        <Card className="p-10 text-center text-sm text-faint">Chưa có voucher nào.</Card>
      ) : (
        <Card className="overflow-x-auto">
          <table className="w-full min-w-[860px] text-sm">
            <thead>
              <tr className="border-b border-ink-700 text-left text-[11px] uppercase tracking-wide text-faint">
                <th className="px-5 py-3.5 font-semibold">Voucher</th>
                <th className="px-5 py-3.5 font-semibold">Mức giảm</th>
                <th className="px-5 py-3.5 font-semibold">Đơn tối thiểu</th>
                <th className="px-5 py-3.5 font-semibold">Thời gian áp dụng</th>
                <th className="px-5 py-3.5 font-semibold">Đã dùng</th>
                <th className="px-5 py-3.5 font-semibold">Trạng thái</th>
                <th className="px-5 py-3.5" />
              </tr>
            </thead>
            <tbody>
              {data.map((p) => {
                const status = statusOf(p)
                return (
                  <tr key={p.id} className="border-b border-ink-800 last:border-0">
                    <td className="px-5 py-3">
                      <p className="font-mono font-bold">{p.code}</p>
                      <p className="text-[12px] text-faint">{p.name}</p>
                    </td>
                    <td className="px-5 py-3 tabular">{discountText(p)}</td>
                    <td className="px-5 py-3 tabular">{money(p.minOrderAmount)}</td>
                    <td className="px-5 py-3 text-[12px] text-muted">
                      <p>Từ {fullDateTime(p.startsAtUtc)}</p>
                      <p>Đến {fullDateTime(p.endsAtUtc)}</p>
                    </td>
                    <td className="px-5 py-3 tabular">
                      {p.usedCount} / {p.usageLimit ?? '∞'}
                      <p className="text-[11px] text-faint">mỗi khách {p.perUserLimit} lần</p>
                    </td>
                    <td className="px-5 py-3"><Badge tone={status.tone}>{status.label}</Badge></td>
                    <td className="px-5 py-3">
                      <div className="flex justify-end gap-1">
                        <button onClick={() => toggle.mutate(p)} disabled={toggle.isPending}
                                title={p.isActive ? 'Tắt voucher' : 'Bật voucher'}
                                className="rounded-lg p-2 text-faint transition-colors hover:bg-ink-800 hover:text-cream">
                          <Power size={15} />
                        </button>
                        <button onClick={() => openEdit(p)} title="Sửa"
                                className="rounded-lg p-2 text-faint transition-colors hover:bg-ink-800 hover:text-cream">
                          <Pencil size={15} />
                        </button>
                        <button onClick={() => confirmRemove(p)} disabled={remove.isPending}
                                title={p.locked ? 'Đã có đơn dùng — bấm để tắt' : 'Xoá'}
                                className="rounded-lg p-2 text-faint transition-colors hover:bg-ink-800 hover:text-velvet-400">
                          <Trash2 size={15} />
                        </button>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </Card>
      )}

      <Modal open={open} onClose={close} wide
             title={editing ? `Sửa voucher ${editing.code}` : 'Tạo voucher mới'}>
        <form onSubmit={(e) => { e.preventDefault(); save.mutate() }} className="flex flex-col gap-4">
          {error && <ErrorNote>{error}</ErrorNote>}
          {locked && (
            <p className="text-[13px] text-faint">
              Voucher đã có đơn sử dụng nên không đổi được mã, loại giảm và giá trị.
            </p>
          )}

          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Mã voucher" hint="3–32 ký tự: chữ không dấu, số, - hoặc _. Tự viết hoa.">
              <Input required value={form.code} onChange={set('code')} disabled={locked}
                     pattern="[A-Za-z0-9_\-]{3,32}" className="font-mono" />
            </Field>
            <Field label="Tên hiển thị">
              <Input required maxLength={128} value={form.name} onChange={set('name')}
                     placeholder="Ví dụ: Thứ ba vui vẻ — giảm 30.000đ" />
            </Field>

            <Field label="Loại giảm">
              <Select value={form.discountType} onChange={set('discountType')} disabled={locked}>
                <option value="Percentage">Theo phần trăm</option>
                <option value="FixedAmount">Số tiền cố định</option>
              </Select>
            </Field>
            <Field label={isPercent ? 'Phần trăm giảm (%)' : 'Số tiền giảm (đồng)'}>
              <Input type="number" required disabled={locked}
                     min={isPercent ? 1 : 1000} max={isPercent ? 100 : undefined}
                     step={isPercent ? 1 : 1000} value={form.value} onChange={set('value')} />
            </Field>

            {isPercent && (
              <Field label="Giảm tối đa (đồng)" hint="Để trống nếu không giới hạn">
                <Input type="number" min={1000} step={1000} value={form.maxDiscount}
                       onChange={set('maxDiscount')} />
              </Field>
            )}
            <Field label="Đơn tối thiểu (đồng)" hint="Tính trên tiền ghế cộng bắp nước">
              <Input type="number" required min={0} step={1000} value={form.minOrderAmount}
                     onChange={set('minOrderAmount')} />
            </Field>

            <Field label="Bắt đầu (giờ Việt Nam)">
              <Input type="datetime-local" required value={form.startsLocal} onChange={set('startsLocal')} />
            </Field>
            <Field label="Kết thúc (giờ Việt Nam)">
              <Input type="datetime-local" required value={form.endsLocal} onChange={set('endsLocal')} />
            </Field>

            <Field label="Tổng lượt dùng"
                   hint={editing ? `Đã dùng ${editing.usedCount}. Để trống nếu không giới hạn` : 'Để trống nếu không giới hạn'}>
              <Input type="number" min={Math.max(1, editing?.usedCount ?? 0)} value={form.usageLimit}
                     onChange={set('usageLimit')} />
            </Field>
            <Field label="Số lượt mỗi khách">
              <Input type="number" required min={1} value={form.perUserLimit} onChange={set('perUserLimit')} />
            </Field>
          </div>

          <label className="flex items-center gap-2.5 text-sm text-muted">
            <input type="checkbox" checked={form.isActive}
                   onChange={(e) => setForm((s) => ({ ...s, isActive: e.target.checked }))}
                   className="h-4 w-4 accent-[#F0A62F]" />
            Đang áp dụng
          </label>

          <div className="flex justify-end gap-2 pt-1">
            <Button type="button" variant="ghost" onClick={close}>Đóng</Button>
            <Button type="submit" loading={save.isPending}>{editing ? 'Lưu thay đổi' : 'Tạo voucher'}</Button>
          </div>
        </form>
      </Modal>
    </div>
  )
}
