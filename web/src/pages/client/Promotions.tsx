import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { BookmarkPlus, Check, Copy, Plus, Tag, Ticket, X } from 'lucide-react'
import clsx from 'clsx'
import { api, ApiError } from '@/lib/api'
import { useAuth } from '@/lib/auth'
import type { Promotion, Voucher } from '@/lib/types'
import { fullDateTime, money } from '@/lib/format'
import { Button, EmptyState, ErrorNote, Input, SectionHeading, Skeleton } from '@/components/ui'

export default function Promotions() {
  const { user } = useAuth()
  const qc = useQueryClient()
  const [manual, setManual] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [adding, setAdding] = useState(false)

  // Đã đăng nhập thì lấy bản có kèm trạng thái "đã lưu"; chưa thì chỉ xem danh sách chung.
  const { data, isLoading } = useQuery({
    queryKey: ['vouchers', 'page', !!user],
    queryFn: () => user
      ? api.get<Voucher[]>('/api/promotions/for-order?amount=0')
      : api.get<Promotion[]>('/api/promotions').then((list) =>
          list.map((p) => ({ ...p, saved: false, usable: true, reason: null, discountAmount: 0 }) as Voucher)),
  })

  const invalidate = () => qc.invalidateQueries({ queryKey: ['vouchers'] })

  const toggle = useMutation({
    mutationFn: (v: Voucher) =>
      v.saved ? api.del(`/api/promotions/${v.id}/save`) : api.post(`/api/promotions/${v.id}/save`),
    onSuccess: invalidate,
  })

  const addByCode = useMutation({
    mutationFn: (code: string) => api.post('/api/promotions/save-by-code', { code }),
    onSuccess: () => { setManual(''); setAdding(false); setError(null); invalidate() },
    onError: (e: ApiError) => setError(e.message),
  })

  const saved = (data ?? []).filter((v) => v.saved)
  const others = (data ?? []).filter((v) => !v.saved)

  return (
    <div className="mx-auto max-w-4xl px-4 py-10">
      <SectionHeading
        eyebrow="Khuyến mãi" title="Voucher"
        description={user
          ? 'Lưu voucher vào kho, tới bước đặt vé chỉ việc bấm chọn — không cần nhớ mã.'
          : 'Đăng nhập để lưu voucher vào kho và dùng nhanh khi đặt vé.'}
        action={user && !adding
          ? <Button variant="secondary" size="sm" onClick={() => setAdding(true)}>
              <Plus size={14} /> Thêm bằng mã
            </Button>
          : undefined}
      />

      {adding && (
        <div className="mb-6 flex flex-col gap-2">
          <div className="flex gap-2">
            <Input value={manual} autoFocus className="font-mono uppercase"
                   placeholder="Nhập mã voucher"
                   onChange={(e) => { setManual(e.target.value.toUpperCase()); setError(null) }}
                   onKeyDown={(e) => { if (e.key === 'Enter' && manual.trim()) addByCode.mutate(manual) }} />
            <Button loading={addByCode.isPending} disabled={!manual.trim()}
                    onClick={() => addByCode.mutate(manual)}>Lưu vào kho</Button>
            <Button variant="ghost" onClick={() => { setAdding(false); setError(null) }}><X size={15} /></Button>
          </div>
          {error && <ErrorNote>{error}</ErrorNote>}
        </div>
      )}

      {isLoading ? (
        <div className="grid gap-4 sm:grid-cols-2">
          {[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-52 rounded-[14px]" />)}
        </div>
      ) : !data?.length ? (
        <EmptyState icon={<Tag size={38} />} title="Hiện chưa có ưu đãi nào"
                    description="Ghé lại sau, rạp thường mở mã mới vào đầu tháng." />
      ) : (
        <div className="flex flex-col gap-8">
          {saved.length > 0 && (
            <section>
              <h2 className="mb-3 flex items-center gap-2 text-[13px] font-semibold uppercase tracking-wide text-faint">
                <Ticket size={14} className="text-brand-500" /> Kho voucher của bạn ({saved.length})
              </h2>
              <div className="grid gap-4 sm:grid-cols-2">
                {saved.map((v) => (
                  <VoucherCard key={v.id} voucher={v} canSave={!!user}
                               onToggle={() => toggle.mutate(v)} />
                ))}
              </div>
            </section>
          )}

          {others.length > 0 && (
            <section>
              <h2 className="mb-3 flex items-center gap-2 text-[13px] font-semibold uppercase tracking-wide text-faint">
                <Tag size={14} /> {saved.length > 0 ? 'Ưu đãi khác' : 'Ưu đãi đang chạy'}
              </h2>
              <div className="grid gap-4 sm:grid-cols-2">
                {others.map((v) => (
                  <VoucherCard key={v.id} voucher={v} canSave={!!user}
                               onToggle={() => toggle.mutate(v)} />
                ))}
              </div>
            </section>
          )}
        </div>
      )}
    </div>
  )
}

/** Thẻ voucher dựng theo hình cuống vé, có nút lưu vào kho và nút chép mã. */
function VoucherCard({ voucher: v, canSave, onToggle }: {
  voucher: Voucher; canSave: boolean; onToggle: () => void
}) {
  const [copied, setCopied] = useState(false)

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(v.code)
      setCopied(true)
      setTimeout(() => setCopied(false), 1600)
    } catch { /* trình duyệt chặn clipboard, khách tự chép tay */ }
  }

  return (
    <div className={clsx('lift surface flex flex-col overflow-hidden',
      v.saved && 'border-brand-600/45')}>
      <div className="relative flex items-start gap-4 p-5">
        <div className="absolute inset-0 -z-10 screen-glow opacity-60" />
        <div className={clsx('flex h-11 w-11 shrink-0 items-center justify-center rounded-xl',
          v.saved ? 'bg-brand-500 text-on-brand' : 'bg-brand-950 text-brand-500')}>
          <Tag size={19} />
        </div>
        <div className="min-w-0 flex-1">
          <h3 className="font-display text-lg font-bold leading-snug">{v.name}</h3>
          <p className="mt-1 text-[13px] text-faint">
            {v.discountType === 'Percentage'
              ? `Giảm ${v.value}%${v.maxDiscount ? `, tối đa ${money(v.maxDiscount)}` : ''}`
              : `Giảm ${money(v.value)}`}
          </p>
        </div>
        {canSave && (
          <button onClick={onToggle}
                  title={v.saved ? 'Bỏ khỏi kho' : 'Lưu vào kho'}
                  aria-label={v.saved ? 'Bỏ khỏi kho' : 'Lưu vào kho'}
                  className={clsx('shrink-0 rounded-lg border p-2 transition-colors',
                    v.saved ? 'border-brand-600/45 bg-brand-950 text-brand-500 hover:border-velvet-500 hover:text-velvet-500'
                            : 'border-ink-600 text-faint hover:border-brand-500 hover:text-brand-500')}>
            {v.saved ? <Check size={15} /> : <BookmarkPlus size={15} />}
          </button>
        )}
      </div>

      <div className="relative flex items-center">
        <span className="h-4 w-4 shrink-0 -translate-x-1/2 rounded-full bg-ink-950" />
        <span className="dashed-cut h-px flex-1" />
        <span className="h-4 w-4 shrink-0 translate-x-1/2 rounded-full bg-ink-950" />
      </div>

      <div className="flex flex-1 flex-col gap-3 p-5">
        <button onClick={copy}
                className="group flex items-center justify-between gap-3 rounded-lg border border-dashed border-brand-600/50
                           bg-brand-950 px-3.5 py-2 transition-colors hover:border-brand-500">
          <code className="font-mono text-sm font-bold tracking-[0.12em] text-brand-500">{v.code}</code>
          <span className="flex items-center gap-1 text-[11px] text-faint transition-colors group-hover:text-brand-500">
            <Copy size={12} /> {copied ? 'Đã chép' : 'Chép mã'}
          </span>
        </button>

        <div className="mt-auto text-[12px] text-faint">
          <p>Đơn tối thiểu {money(v.minOrderAmount)}</p>
          <p>Hạn dùng đến {fullDateTime(v.endsAtUtc)}</p>
        </div>
      </div>
    </div>
  )
}
