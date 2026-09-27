import { useState } from 'react'
import { Link, useNavigate, useSearchParams } from 'react-router-dom'
import { Clapperboard } from 'lucide-react'
import { useAuth } from '@/lib/auth'
import { ApiError } from '@/lib/api'
import { Button, Card, ErrorNote, Field, Input } from '@/components/ui'

const DEMO = [
  ['khach@gmail.com', 'Khách hàng'],
  ['admin@cinebook.vn', 'Quản trị hệ thống'],
  ['manager@cinebook.vn', 'Quản lý rạp'],
  ['staff@cinebook.vn', 'Nhân viên soát vé'],
]

export function Login() {
  const { login } = useAuth()
  const navigate = useNavigate()
  const [params] = useSearchParams()
  const next = params.get('next') ?? '/'

  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true); setError(null)
    try {
      const user = await login(email, password)
      navigate(user.role === 'Staff' ? '/soat-ve'
        : user.role === 'Admin' || user.role === 'CinemaManager' ? '/admin'
        : next, { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Không đăng nhập được.')
    } finally { setLoading(false) }
  }

  return (
    <AuthShell title="Đăng nhập" subtitle="Đăng nhập để đặt vé và quản lý vé của bạn">
      <form onSubmit={submit} className="flex flex-col gap-4">
        {error && <ErrorNote>{error}</ErrorNote>}
        <Field label="Email">
          <Input type="email" required autoComplete="email" value={email}
                 onChange={(e) => setEmail(e.target.value)} placeholder="ban@email.com" />
        </Field>
        <Field label="Mật khẩu">
          <Input type="password" required autoComplete="current-password" value={password}
                 onChange={(e) => setPassword(e.target.value)} placeholder="••••••" />
        </Field>
        <Button type="submit" size="lg" loading={loading}>Đăng nhập</Button>
      </form>

      <p className="mt-5 text-center text-[13px] text-faint">
        Chưa có tài khoản? <Link to="/dang-ky" className="text-brand-400 hover:underline">Đăng ký ngay</Link>
      </p>

      <div className="mt-6 rounded-xl border border-ink-700 bg-ink-900 p-4">
        <p className="mb-2.5 text-[12px] font-semibold text-muted">Tài khoản dùng thử — mật khẩu 123456</p>
        <div className="flex flex-col gap-1.5">
          {DEMO.map(([mail, label]) => (
            <button key={mail} type="button"
                    onClick={() => { setEmail(mail); setPassword('123456') }}
                    className="flex items-center justify-between rounded-lg px-2.5 py-1.5 text-left text-[12px]
                               text-faint transition-colors hover:bg-ink-800 hover:text-cream">
              <span className="font-mono">{mail}</span>
              <span>{label}</span>
            </button>
          ))}
        </div>
      </div>
    </AuthShell>
  )
}

export function Register() {
  const { register } = useAuth()
  const navigate = useNavigate()
  const [form, setForm] = useState({ email: '', password: '', fullName: '', phone: '' })
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setLoading(true); setError(null)
    try {
      await register(form)
      navigate('/', { replace: true })
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Không đăng ký được.')
    } finally { setLoading(false) }
  }

  const set = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) =>
    setForm((s) => ({ ...s, [k]: e.target.value }))

  return (
    <AuthShell title="Tạo tài khoản" subtitle="Chỉ mất chưa tới một phút">
      <form onSubmit={submit} className="flex flex-col gap-4">
        {error && <ErrorNote>{error}</ErrorNote>}
        <Field label="Họ và tên">
          <Input required value={form.fullName} onChange={set('fullName')} placeholder="Nguyễn Văn A" />
        </Field>
        <Field label="Email">
          <Input type="email" required autoComplete="email" value={form.email}
                 onChange={set('email')} placeholder="ban@email.com" />
        </Field>
        <Field label="Số điện thoại" hint="Không bắt buộc">
          <Input value={form.phone} onChange={set('phone')} placeholder="09xx xxx xxx" />
        </Field>
        <Field label="Mật khẩu" hint="Ít nhất 6 ký tự">
          <Input type="password" required minLength={6} autoComplete="new-password"
                 value={form.password} onChange={set('password')} placeholder="••••••" />
        </Field>
        <Button type="submit" size="lg" loading={loading}>Đăng ký</Button>
      </form>

      <p className="mt-5 text-center text-[13px] text-faint">
        Đã có tài khoản? <Link to="/dang-nhap" className="text-brand-400 hover:underline">Đăng nhập</Link>
      </p>
    </AuthShell>
  )
}

function AuthShell({ title, subtitle, children }: {
  title: string; subtitle: string; children: React.ReactNode
}) {
  return (
    <div className="relative mx-auto max-w-md px-4 py-14">
      <div className="pointer-events-none absolute inset-x-0 top-0 h-64 screen-glow" />
      <div className="relative">
        <div className="mb-7 text-center">
          <Clapperboard size={30} className="mx-auto mb-3 text-brand-500" />
          <h1 className="font-display text-2xl font-extrabold">{title}</h1>
          <p className="mt-1.5 text-[13px] text-faint">{subtitle}</p>
        </div>
        <Card className="p-6">{children}</Card>
      </div>
    </div>
  )
}
