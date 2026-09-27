import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BookmarkPlus, Check, Info, Plus, Tag, Ticket, X } from 'lucide-react'
import clsx from 'clsx'
import { api, ApiError } from '@/lib/api'
import type { Voucher } from '@/lib/types'
import { money } from '@/lib/format'
import { Button, ErrorNote, Input, Skeleton } from '@/components/ui'

/**
 * Chọn voucher bằng cách bấm thay vì gõ mã.
 *
 * Máy chủ trả về sẵn số tiền được giảm cho đúng đơn này và lý do nếu chưa dùng
 * được, nên khách thấy trước con số thật chứ không phải thử rồi mới biết.
 * Voucher đã lưu trong kho được đánh dấu và xếp lên trước.
 */
export function VoucherPicker({ amount, value, onChange }: {
  amount: number
  value: string | null
  onChange: (code: string | null) => void
}) {
  const qc = useQueryClient()
  const [manual, setManual] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [showInput, setShowInput] = useState(false)

  const key = ['vouchers', 'for-order', amount]
  const { data, isLoading } = useQuery({
    queryKey: key,
    queryFn: () => api.get<Voucher[]>(`/api/promotions/for-order?amount=${amount}`),
  })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['vouchers'] })

  const saveByCode = useMutation({
    mutationFn: (code: string) => api.post('/api/promotions/save-by-code', { code }),
    onSuccess: (_r, code) => {
      setError(null); setManual(''); setShowInput(false)
      onChange(code.trim().toUpperCase())
      invalidate()
    },
    onError: (e: ApiError) => setError(e.message),
  })

  const toggleSaved = useMutation({
    mutationFn: (v: Voucher) =>
      v.saved ? api.del(`/api/promotions/${v.id}/save`) : api.post(`/api/promotions/${v.id}/save`),
    onSuccess: invalidate,
  })

  if (isLoading) {
    return (
      <div className="flex flex-col gap-2.5">
        {[0, 1].map((i) => <Skeleton key={i} className="h-20 rounded-xl" />)}
      </div>
    )
  }

  const vouchers = data ?? []
  const saved = vouchers.filter((v) => v.saved)
  const others = vouchers.filter((v) => !v.saved)

  return (
    <div className="flex flex-col gap-4">
      {vouchers.length === 0 && (
        <p className="rounded-xl border border-dashed border-ink-700 px-4 py-6 text-center text-[13px] text-faint">
          Chưa có voucher nào khả dụng.
        </p>
      )}

      {saved.length > 0 && (
        <Group icon={<Ticket size={13} />} title={`Kho voucher của bạn (${saved.length})`}>
          {saved.map((v) => (
            <VoucherRow key={v.id} voucher={v} active={v.code === value}
                        onPick={() => onChange(v.code === value ? null : v.code)}
                        onToggleSave={() => toggleSaved.mutate(v)} />
          ))}
        </Group>
      )}

      {others.length > 0 && (
        <Group icon={<Tag size={13} />} title="Ưu đãi đang chạy">
          {others.map((v) => (
            <VoucherRow key={v.id} voucher={v} active={v.code === value}
                        onPick={() => onChange(v.code === value ? null : v.code)}
                        onToggleSave={() => toggleSaved.mutate(v)} />
          ))}
        </Group>
      )}

      {/* Vẫn giữ đường nhập mã tay cho voucher không nằm trong danh sách công khai */}
      {showInput ? (
        <div className="flex flex-col gap-2">
          <div className="flex gap-2">
            <Input
              value={manual}
              onChange={(e) => { setManual(e.target.value.toUpperCase()); setError(null) }}
              onKeyDown={(e) => { if (e.key === 'Enter' && manual.trim()) saveByCode.mutate(manual) }}
              placeholder="Nhập mã, ví dụ CHAOBAN"
              className="font-mono uppercase"
              autoFocus
            />
            <Button variant="secondary" loading={saveByCode.isPending}
                    disabled={!manual.trim()} onClick={() => saveByCode.mutate(manual)}>
              Thêm
            </Button>
            <Button variant="ghost" onClick={() => { setShowInput(false); setError(null) }}>
              <X size={15} />
            </Button>
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
      ) : (
        <button onClick={() => setShowInput(true)}
                className="flex items-center gap-1.5 self-start text-[13px] text-brand-500 transition-colors hover:underline">
          <Plus size={14} /> Tôi có mã khác
        </button>
      )}
    </div>
  )
}

function Group({ icon, title, children }: {
  icon: React.ReactNode; title: string; children: React.ReactNode
}) {
  return (
    <div>
      <p className="mb-2.5 flex items-center gap-1.5 text-[12px] font-semibold uppercase tracking-wide text-faint">
        {icon} {title}
      </p>
      <div className="flex flex-col gap-2.5">{children}</div>
    </div>
  )
}

function VoucherRow({ voucher: v, active, onPick, onToggleSave }: {
  voucher: Voucher; active: boolean; onPick: () => void; onToggleSave: () => void
}) {
  return (
    <div className={clsx(
      'relative flex items-stretch overflow-hidden rounded-xl border transition-all',
      active ? 'border-brand-500 bg-brand-950/70 shadow-[0_0_0_3px_rgba(229, 9, 20,.12)]'
             : v.usable ? 'border-ink-700 bg-ink-850 hover:border-ink-600'
                        : 'border-ink-700 bg-ink-900 opacity-70',
    )}>
      <button
        onClick={onPick}
        disabled={!v.usable}
        aria-pressed={active}
        className="flex min-w-0 flex-1 items-center gap-3.5 p-3.5 text-left disabled:cursor-not-allowed"
      >
        <span className={clsx(
          'flex h-10 w-10 shrink-0 items-center justify-center rounded-lg',
          active ? 'bg-brand-500 text-on-brand' : v.usable ? 'bg-brand-950 text-brand-500' : 'bg-ink-800 text-ink-500',
        )}>
          {active ? <Check size={17} /> : <Tag size={16} />}
        </span>

        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-baseline gap-x-2">
            <span className="truncate text-sm font-semibold">{v.name}</span>
            <code className="font-mono text-[11px] tracking-wider text-faint">{v.code}</code>
          </span>

          {v.usable ? (
            <span className="mt-0.5 block text-[13px] font-semibold text-mint-500">
              Giảm {money(v.discountAmount)}
            </span>
          ) : (
            <span className="mt-0.5 flex items-start gap-1 text-[12px] text-faint">
              <Info size={12} className="mt-0.5 shrink-0" /> {v.reason}
            </span>
          )}
        </span>
      </button>

      <button
        onClick={onToggleSave}
        title={v.saved ? 'Bỏ khỏi kho voucher' : 'Lưu vào kho voucher'}
        aria-label={v.saved ? 'Bỏ khỏi kho voucher' : 'Lưu vào kho voucher'}
        className={clsx(
          'flex w-11 shrink-0 items-center justify-center border-l transition-colors',
          v.saved ? 'border-brand-600/30 text-brand-500 hover:bg-brand-950'
                  : 'border-ink-700 text-faint hover:bg-ink-800 hover:text-brand-500',
        )}
      >
        {v.saved ? <Ticket size={15} /> : <BookmarkPlus size={15} />}
      </button>
    </div>
  )
}
