import { useEffect, useState } from 'react'
import { Timer } from 'lucide-react'
import clsx from 'clsx'
import { countdown } from '@/lib/format'

/** Đồng hồ đếm ngược thời gian giữ ghế (BR-02). Chuyển đỏ khi còn dưới một phút. */
export function Countdown({ expiresAtUtc, onExpire }: {
  expiresAtUtc: string; onExpire?: () => void
}) {
  const [left, setLeft] = useState(() => secondsLeft(expiresAtUtc))

  useEffect(() => {
    setLeft(secondsLeft(expiresAtUtc))
    const t = setInterval(() => {
      const next = secondsLeft(expiresAtUtc)
      setLeft(next)
      if (next <= 0) { clearInterval(t); onExpire?.() }
    }, 1000)
    return () => clearInterval(t)
  }, [expiresAtUtc, onExpire])

  const urgent = left <= 60

  return (
    <div className={clsx(
      'flex items-center gap-2 rounded-xl border px-3.5 py-2',
      urgent ? 'border-velvet-500/45 bg-velvet-950' : 'border-brand-600/40 bg-brand-950',
    )}>
      <Timer size={15} className={urgent ? 'text-velvet-400' : 'text-brand-400'} />
      <div className="leading-tight">
        <p className={clsx('font-mono text-lg font-bold tabular',
          urgent ? 'text-velvet-400' : 'text-brand-400')}>
          {countdown(left)}
        </p>
        <p className="text-[11px] text-faint">thời gian giữ ghế còn lại</p>
      </div>
    </div>
  )
}

function secondsLeft(iso: string) {
  return Math.max(0, Math.floor((new Date(iso).getTime() - Date.now()) / 1000))
}
