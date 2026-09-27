import { useState } from 'react'
import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import { Clapperboard, LayoutDashboard, LogOut, Menu, ScanLine, Ticket, X } from 'lucide-react'
import clsx from 'clsx'
import { useAuth } from '@/lib/auth'
import { Button, ThemeToggle } from '@/components/ui'

const NAV = [
  { to: '/', label: 'Trang chủ', end: true },
  { to: '/phim', label: 'Phim' },
  { to: '/rap', label: 'Rạp chiếu' },
  { to: '/uu-dai', label: 'Ưu đãi' },
]

export default function ClientLayout() {
  const { user, logout, hasRole } = useAuth()
  const [open, setOpen] = useState(false)
  const navigate = useNavigate()

  const linkClass = ({ isActive }: { isActive: boolean }) => clsx(
    'relative py-1.5 text-sm transition-colors',
    isActive ? 'text-cream font-medium' : 'text-muted hover:text-cream',
    isActive && 'after:absolute after:-bottom-px after:left-0 after:h-[2px] after:w-full after:rounded-full after:bg-brand-500',
  )

  return (
    <div className="flex min-h-screen flex-col">
      <header className="sticky top-0 z-40 border-b border-ink-800 bg-ink-950/85 backdrop-blur-xl">
        <div aria-hidden className="absolute inset-x-0 -bottom-px h-px bg-gradient-to-r from-transparent via-brand-600/45 to-transparent" />
        <div className="mx-auto flex h-16 max-w-6xl items-center gap-7 px-4">
          <Link to="/" className="flex shrink-0 items-center gap-2">
            <Clapperboard size={22} className="text-brand-500" />
            <span className="font-display text-lg font-extrabold tracking-tight">CineBook</span>
          </Link>

          <nav className="hidden items-center gap-6 md:flex">
            {NAV.map((n) => (
              <NavLink key={n.to} to={n.to} end={n.end} className={linkClass}>{n.label}</NavLink>
            ))}
          </nav>

          <div className="ml-auto flex items-center gap-2">
            <ThemeToggle />
            {hasRole('Staff', 'CinemaManager', 'Admin') && (
              <Button size="sm" variant="ghost" onClick={() => navigate('/soat-ve')}
                      className="hidden sm:inline-flex">
                <ScanLine size={15} /> Soát vé
              </Button>
            )}
            {hasRole('CinemaManager', 'Admin') && (
              <Button size="sm" variant="ghost" onClick={() => navigate('/admin')}
                      className="hidden sm:inline-flex">
                <LayoutDashboard size={15} /> Quản trị
              </Button>
            )}

            {user ? (
              <div className="hidden items-center gap-2 sm:flex">
                <Link to="/ve-cua-toi"
                      className="flex items-center gap-1.5 rounded-lg px-3 py-2 text-sm text-muted transition-colors hover:bg-ink-800 hover:text-cream">
                  <Ticket size={15} /> Vé của tôi
                </Link>
                <div className="flex items-center gap-2 rounded-lg border border-ink-700 bg-ink-850 py-1.5 pl-2.5 pr-1.5">
                  <span className="max-w-28 truncate text-[13px] text-muted">{user.fullName}</span>
                  <button onClick={logout} title="Đăng xuất"
                          className="rounded-md p-1.5 text-faint transition-colors hover:bg-ink-700 hover:text-velvet-400">
                    <LogOut size={14} />
                  </button>
                </div>
              </div>
            ) : (
              <div className="hidden items-center gap-2 sm:flex">
                <Button size="sm" variant="ghost" onClick={() => navigate('/dang-nhap')}>Đăng nhập</Button>
                <Button size="sm" onClick={() => navigate('/dang-ky')}>Đăng ký</Button>
              </div>
            )}

            <button onClick={() => setOpen((v) => !v)}
                    className="rounded-lg p-2 text-muted transition-colors hover:bg-ink-800 md:hidden">
              {open ? <X size={19} /> : <Menu size={19} />}
            </button>
          </div>
        </div>

        {open && (
          <div className="border-t border-ink-800 bg-ink-950 px-4 py-3 md:hidden">
            <div className="flex flex-col gap-1">
              {NAV.map((n) => (
                <NavLink key={n.to} to={n.to} end={n.end} onClick={() => setOpen(false)}
                         className="rounded-lg px-3 py-2.5 text-sm text-muted hover:bg-ink-800 hover:text-cream">
                  {n.label}
                </NavLink>
              ))}
              <div className="my-2 h-px bg-ink-800" />
              {user ? (
                <>
                  <Link to="/ve-cua-toi" onClick={() => setOpen(false)}
                        className="flex items-center gap-2 rounded-lg px-3 py-2.5 text-sm text-muted hover:bg-ink-800">
                    <Ticket size={15} /> Vé của tôi
                  </Link>
                  {hasRole('Staff', 'CinemaManager', 'Admin') && (
                    <Link to="/soat-ve" onClick={() => setOpen(false)}
                          className="flex items-center gap-2 rounded-lg px-3 py-2.5 text-sm text-muted hover:bg-ink-800">
                      <ScanLine size={15} /> Soát vé
                    </Link>
                  )}
                  {hasRole('CinemaManager', 'Admin') && (
                    <Link to="/admin" onClick={() => setOpen(false)}
                          className="flex items-center gap-2 rounded-lg px-3 py-2.5 text-sm text-muted hover:bg-ink-800">
                      <LayoutDashboard size={15} /> Trang quản trị
                    </Link>
                  )}
                  <button onClick={() => { logout(); setOpen(false) }}
                          className="flex items-center gap-2 rounded-lg px-3 py-2.5 text-left text-sm text-velvet-400 hover:bg-ink-800">
                    <LogOut size={15} /> Đăng xuất
                  </button>
                </>
              ) : (
                <div className="flex gap-2 pt-1">
                  <Button size="sm" variant="secondary" className="flex-1"
                          onClick={() => { setOpen(false); navigate('/dang-nhap') }}>Đăng nhập</Button>
                  <Button size="sm" className="flex-1"
                          onClick={() => { setOpen(false); navigate('/dang-ky') }}>Đăng ký</Button>
                </div>
              )}
            </div>
          </div>
        )}
      </header>

      <main className="flex-1"><Outlet /></main>

      <footer className="mt-20 border-t border-ink-800 bg-ink-900">
        <div className="mx-auto grid max-w-6xl gap-8 px-4 py-12 sm:grid-cols-2 lg:grid-cols-4">
          <div>
            <div className="mb-3 flex items-center gap-2">
              <Clapperboard size={18} className="text-brand-500" />
              <span className="font-display font-extrabold">CineBook</span>
            </div>
            <p className="text-[13px] leading-relaxed text-faint">
              Đặt vé xem phim trực tuyến, chọn ghế theo thời gian thực, nhận vé điện tử ngay sau khi thanh toán.
            </p>
          </div>
          <FooterCol title="Khám phá" items={[
            { label: 'Phim đang chiếu', to: '/phim' },
            { label: 'Phim sắp chiếu', to: '/phim' },
            { label: 'Rạp chiếu', to: '/rap' },
            { label: 'Ưu đãi', to: '/uu-dai' },
          ]} />
          <FooterCol title="Hỗ trợ" items={[
            { label: 'Vé của tôi', to: '/ve-cua-toi' },
            { label: 'Chính sách hoàn vé', to: '/uu-dai' },
            { label: 'Hệ thống rạp', to: '/rap' },
          ]} />
          <div>
            <p className="mb-3 text-[13px] font-semibold">Tổng đài</p>
            <p className="font-display text-2xl font-extrabold text-gradient-brand">1900 6017</p>
            <p className="mt-1 text-[13px] text-faint">8:00 – 22:00 hằng ngày</p>
          </div>
        </div>
        <div className="border-t border-ink-800 px-4 py-5 text-center text-[12px] text-faint">
          Đồ án môn Phân tích thiết kế hướng đối tượng — Trường Đại học Giao thông Vận tải
        </div>
      </footer>
    </div>
  )
}

function FooterCol({ title, items }: {
  title: string; items: { label: string; to: string }[]
}) {
  return (
    <div>
      <p className="mb-3 text-[13px] font-semibold">{title}</p>
      <ul className="flex flex-col gap-2 text-[13px] text-faint">
        {items.map((i) => (
          <li key={i.label}>
            <Link to={i.to} className="transition-colors hover:text-brand-400">{i.label}</Link>
          </li>
        ))}
      </ul>
    </div>
  )
}
