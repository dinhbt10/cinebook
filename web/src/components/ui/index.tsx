import clsx from 'clsx'
import { Loader2, Moon, Sun } from 'lucide-react'
import { useTheme } from '@/lib/theme'
import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode,
  SelectHTMLAttributes, TextareaHTMLAttributes } from 'react'

/* ----------------------------------------------------------- Button */

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'
type Size = 'sm' | 'md' | 'lg'

const VARIANT: Record<Variant, string> = {
  primary: 'bg-brand-500 text-on-brand hover:bg-brand-400 font-semibold shadow-[0_1px_0_rgba(255,255,255,.25)_inset]',
  secondary: 'bg-ink-800 text-cream border border-ink-600 hover:border-ink-500 hover:bg-ink-700',
  ghost: 'text-muted hover:text-cream hover:bg-ink-800',
  danger: 'bg-velvet-500 text-white hover:bg-velvet-400 font-semibold',
}
const SIZE: Record<Size, string> = {
  sm: 'h-9 px-3.5 text-[13px] rounded-lg gap-1.5',
  md: 'h-11 px-5 text-sm rounded-xl gap-2',
  lg: 'h-13 px-7 text-[15px] rounded-xl gap-2',
}

export function Button({
  variant = 'primary', size = 'md', loading, className, children, disabled, ...rest
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant; size?: Size; loading?: boolean
}) {
  return (
    <button
      {...rest}
      disabled={disabled || loading}
      className={clsx(
        'inline-flex items-center justify-center transition-all duration-150',
        'disabled:opacity-45 disabled:cursor-not-allowed active:scale-[.985]',
        VARIANT[variant], SIZE[size], className,
      )}
    >
      {loading && <Loader2 size={15} className="animate-spin" />}
      {children}
    </button>
  )
}

/** Nút chuyển giữa nền sáng và nền tối. */
export function ThemeToggle({ className }: { className?: string }) {
  const { theme, toggle } = useTheme()
  const dark = theme === 'dark'
  return (
    <button
      onClick={toggle}
      title={dark ? 'Chuyển sang nền sáng' : 'Chuyển sang nền tối'}
      aria-label={dark ? 'Chuyển sang nền sáng' : 'Chuyển sang nền tối'}
      className={clsx(
        'relative flex h-9 w-9 items-center justify-center rounded-lg border border-ink-700',
        'text-muted transition-colors hover:border-ink-600 hover:text-brand-500',
        className,
      )}
    >
      <Sun size={15} className={clsx('absolute transition-all duration-300',
        dark ? 'scale-0 -rotate-90 opacity-0' : 'scale-100 rotate-0 opacity-100')} />
      <Moon size={15} className={clsx('absolute transition-all duration-300',
        dark ? 'scale-100 rotate-0 opacity-100' : 'scale-0 rotate-90 opacity-0')} />
    </button>
  )
}

/* ------------------------------------------------------------ Badge */

type Tone = 'brand' | 'mint' | 'velvet' | 'faint' | 'outline'
const TONE: Record<Tone, string> = {
  brand: 'bg-brand-950 text-brand-400 border-brand-600/40',
  mint: 'bg-mint-950 text-mint-400 border-mint-500/35',
  velvet: 'bg-velvet-950 text-velvet-400 border-velvet-500/35',
  faint: 'bg-ink-800 text-faint border-ink-600',
  outline: 'bg-transparent text-muted border-ink-600',
}

export function Badge({ tone = 'outline', className, children }: {
  tone?: Tone; className?: string; children: ReactNode
}) {
  return (
    <span className={clsx(
      'inline-flex items-center gap-1 rounded-md border px-2 py-[3px] text-[11px] font-medium leading-none',
      TONE[tone], className,
    )}>{children}</span>
  )
}

/* --------------------------------------------------- Card & sections */

export function Card({ className, children }: { className?: string; children: ReactNode }) {
  return <div className={clsx('surface', className)}>{children}</div>
}

export function SectionTitle({ children, action }: { children: ReactNode; action?: ReactNode }) {
  return (
    <div className="mb-4 flex items-end justify-between gap-4">
      <h2 className="text-xl font-bold">{children}</h2>
      {action}
    </div>
  )
}

/**
 * Tiêu đề mục có nhãn nhỏ phía trên. Nhãn cho biết đang ở mạch nội dung nào,
 * nhờ đó các mục nối nhau không còn chỉ là một dãy tiêu đề cùng cỡ.
 */
export function SectionHeading({ eyebrow, title, description, action }: {
  eyebrow?: string; title: ReactNode; description?: string; action?: ReactNode
}) {
  return (
    <div className="mb-6 flex flex-wrap items-end justify-between gap-x-6 gap-y-3">
      <div className="min-w-0">
        {eyebrow && (
          <p className="mb-2 flex items-center gap-2 text-[11px] font-semibold uppercase tracking-[0.18em] text-brand-500">
            <span className="h-px w-6 bg-brand-600/70" />
            {eyebrow}
          </p>
        )}
        <h2 className="font-display text-2xl font-extrabold md:text-[28px]">{title}</h2>
        {description && <p className="mt-1.5 max-w-xl text-sm text-faint">{description}</p>}
      </div>
      {action}
    </div>
  )
}

/* --------------------------------------------------------- Khối chờ dữ liệu */

export const Skeleton = ({ className }: { className?: string }) =>
  <div className={clsx('skeleton rounded-lg', className)} />

/** Lưới poster giả trong lúc chờ API, để trang không nhảy khi dữ liệu về. */
export function PosterGridSkeleton({ count = 5 }: { count?: number }) {
  return (
    <div className="grid grid-cols-2 gap-x-5 gap-y-8 sm:grid-cols-3 lg:grid-cols-5">
      {Array.from({ length: count }, (_, i) => (
        <div key={i} className="flex flex-col gap-3">
          <Skeleton className="aspect-[2/3] w-full rounded-xl" />
          <Skeleton className="h-4 w-4/5" />
          <Skeleton className="h-3 w-3/5" />
        </div>
      ))}
    </div>
  )
}

/* ------------------------------------------------------- Form fields */

export function Field({ label, hint, error, children }: {
  label?: string; hint?: string; error?: string; children: ReactNode
}) {
  return (
    <label className="flex flex-col gap-1.5">
      {label && <span className="text-[13px] font-medium text-muted">{label}</span>}
      {children}
      {error
        ? <span className="text-[12px] text-velvet-400">{error}</span>
        : hint ? <span className="text-[12px] text-faint">{hint}</span> : null}
    </label>
  )
}

const fieldClass =
  'w-full rounded-xl border border-ink-600 bg-ink-900 px-3.5 py-2.5 text-sm text-cream ' +
  'placeholder:text-faint transition-colors focus:border-brand-600 focus:outline-none ' +
  'disabled:opacity-50'

export const Input = ({ className, ...rest }: InputHTMLAttributes<HTMLInputElement>) =>
  <input {...rest} className={clsx(fieldClass, className)} />

export const Textarea = ({ className, ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement>) =>
  <textarea {...rest} className={clsx(fieldClass, 'min-h-24 resize-y', className)} />

export const Select = ({ className, children, ...rest }: SelectHTMLAttributes<HTMLSelectElement>) => (
  <select {...rest} className={clsx(fieldClass, 'appearance-none pr-9 cursor-pointer', className)}>
    {children}
  </select>
)

/* ------------------------------------------------------ Trạng thái */

export function Spinner({ label }: { label?: string }) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 py-16 text-faint">
      <Loader2 className="animate-spin text-brand-500" size={26} />
      {label && <span className="text-sm">{label}</span>}
    </div>
  )
}

export function EmptyState({ icon, title, description, action }: {
  icon?: ReactNode; title: string; description?: string; action?: ReactNode
}) {
  return (
    <div className="flex flex-col items-center justify-center gap-3 rounded-2xl border border-dashed border-ink-700 px-6 py-14 text-center">
      {icon && <div className="text-ink-500">{icon}</div>}
      <div>
        <p className="font-display text-lg font-bold">{title}</p>
        {description && <p className="mt-1 max-w-sm text-sm text-faint">{description}</p>}
      </div>
      {action}
    </div>
  )
}

/** Ô số liệu nhỏ, dùng cho dải thông tin dưới hero. */
export function StatTile({ icon, value, label }: {
  icon?: ReactNode; value: ReactNode; label: string
}) {
  return (
    <div className="flex items-center gap-3">
      {icon && <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-ink-800 text-brand-500">{icon}</span>}
      <div className="min-w-0">
        <p className="font-display text-lg font-extrabold leading-none">{value}</p>
        <p className="mt-1 truncate text-[12px] text-faint">{label}</p>
      </div>
    </div>
  )
}

export function ErrorNote({ children }: { children: ReactNode }) {
  return (
    <div className="rounded-xl border border-velvet-500/35 bg-velvet-950 px-3.5 py-3 text-[13px] text-velvet-400">
      {children}
    </div>
  )
}

/* ------------------------------------------------------------ Modal */

export function Modal({ open, onClose, title, children, wide }: {
  open: boolean; onClose: () => void; title: string; children: ReactNode; wide?: boolean
}) {
  if (!open) return null
  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto p-4 backdrop-blur-sm"
         style={{ background: 'var(--color-scrim)' }}
         onClick={onClose}>
      <div
        className={clsx('surface my-8 w-full animate-rise p-6', wide ? 'max-w-3xl' : 'max-w-lg')}
        onClick={(e) => e.stopPropagation()}
      >
        <h3 className="mb-5 text-lg font-bold">{title}</h3>
        {children}
      </div>
    </div>
  )
}
