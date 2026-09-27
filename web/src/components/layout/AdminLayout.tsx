import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom'
import {
  BarChart3, Clapperboard, Film, LayoutDashboard, LogOut, MonitorPlay, ScanLine, Store, Tag,
} from 'lucide-react'
import clsx from 'clsx'
import { useAuth } from '@/lib/auth'
import { Spinner } from '@/components/ui'

const MENU = [
  { to: '/admin', label: 'Tổng quan', icon: LayoutDashboard, end: true },
  { to: '/admin/phim', label: 'Phim', icon: Film },
  { to: '/admin/suat-chieu', label: 'Suất chiếu', icon: MonitorPlay },
  { to: '/admin/rap', label: 'Rạp & phòng', icon: Store },
  // Voucher áp cho toàn hệ thống nên quản lý rạp không thấy mục này.
  { to: '/admin/khuyen-mai', label: 'Khuyến mãi', icon: Tag, adminOnly: true },
  { to: '/admin/bao-cao', label: 'Báo cáo', icon: BarChart3 },
]

export default function AdminLayout() {
  const { user, loading, logout, hasRole } = useAuth()
  const navigate = useNavigate()
  const menu = MENU.filter((m) => !m.adminOnly || hasRole('Admin'))

  if (loading) return <Spinner label="Đang kiểm tra quyền truy cập…" />

  if (!hasRole('Admin', 'CinemaManager')) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-3 px-4 text-center">
        <h1 className="font-display text-2xl font-bold">Không có quyền truy cập</h1>
        <p className="text-sm text-faint">Trang quản trị chỉ dành cho quản trị viên và quản lý rạp.</p>
        <Link to="/" className="mt-2 text-sm text-brand-400 hover:underline">Về trang chủ</Link>
      </div>
    )
  }

  return (
    <div className="flex min-h-screen">
      <aside className="hidden w-60 shrink-0 flex-col border-r border-ink-800 bg-ink-900 lg:flex">
        <Link to="/" className="flex h-16 items-center gap-2 border-b border-ink-800 px-5">
          <Clapperboard size={20} className="text-brand-500" />
          <span className="font-display font-extrabold">CineBook</span>
          <span className="ml-auto rounded-md bg-ink-800 px-1.5 py-0.5 text-[10px] font-medium text-faint">QUẢN TRỊ</span>
        </Link>

        <nav className="flex flex-1 flex-col gap-1 p-3">
          {menu.map(({ to, label, icon: Icon, end }) => (
            <NavLink key={to} to={to} end={end} className={({ isActive }) => clsx(
              'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors',
              isActive ? 'bg-brand-950 font-medium text-brand-400'
                       : 'text-muted hover:bg-ink-800 hover:text-cream',
            )}>
              <Icon size={16} /> {label}
            </NavLink>
          ))}
          <div className="my-2 h-px bg-ink-800" />
          <NavLink to="/soat-ve" className="flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm text-muted transition-colors hover:bg-ink-800 hover:text-cream">
            <ScanLine size={16} /> Soát vé
          </NavLink>
        </nav>

        <div className="border-t border-ink-800 p-3">
          <div className="mb-2 px-2">
            <p className="truncate text-[13px] font-medium">{user?.fullName}</p>
            <p className="text-[11px] text-faint">
              {user?.role === 'Admin' ? 'Quản trị hệ thống' : 'Quản lý rạp'}
            </p>
          </div>
          <button onClick={() => { logout(); navigate('/') }}
            className="flex w-full items-center gap-3 rounded-lg px-3 py-2.5 text-sm text-muted transition-colors hover:bg-ink-800 hover:text-velvet-400">
            <LogOut size={16} /> Đăng xuất
          </button>
        </div>
      </aside>

      {/* Thanh điều hướng cho màn hình nhỏ */}
      <div className="fixed inset-x-0 bottom-0 z-40 flex border-t border-ink-800 bg-ink-900 lg:hidden">
        {menu.map(({ to, label, icon: Icon, end }) => (
          <NavLink key={to} to={to} end={end} className={({ isActive }) => clsx(
            'flex flex-1 flex-col items-center gap-1 py-2.5 text-[10px]',
            isActive ? 'text-brand-400' : 'text-faint',
          )}>
            <Icon size={17} /> {label}
          </NavLink>
        ))}
      </div>

      <main className="min-w-0 flex-1 pb-20 lg:pb-0"><Outlet /></main>
    </div>
  )
}
