import { lazy, Suspense } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { AuthProvider, useAuth } from '@/lib/auth'
import { ThemeProvider } from '@/lib/theme'
import { Spinner } from '@/components/ui'

import ClientLayout from '@/components/layout/ClientLayout'
import AdminLayout from '@/components/layout/AdminLayout'

import Home from '@/pages/client/Home'
import Movies from '@/pages/client/Movies'
import MovieDetail from '@/pages/client/MovieDetail'
import Booking from '@/pages/client/Booking'
import BookingResult from '@/pages/client/BookingResult'
import MyTickets from '@/pages/client/MyTickets'
import Cinemas from '@/pages/client/Cinemas'
import Promotions from '@/pages/client/Promotions'
import { Login, Register } from '@/pages/client/Auth'
// Trang quét QR và các trang quản trị kéo theo thư viện nặng,
// nên tách riêng để trang khách hàng tải nhanh.
const CheckIn = lazy(() => import('@/pages/staff/CheckIn'))
const Dashboard = lazy(() => import('@/pages/admin/Dashboard'))
const AdminMovies = lazy(() => import('@/pages/admin/Movies'))
const AdminShowtimes = lazy(() => import('@/pages/admin/Showtimes'))
const AdminRooms = lazy(() => import('@/pages/admin/Rooms'))
const AdminReports = lazy(() => import('@/pages/admin/Reports'))
const AdminPromotions = lazy(() => import('@/pages/admin/Promotions'))

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, refetchOnWindowFocus: false, staleTime: 30_000 },
  },
})

/** Chặn các trang cần đăng nhập, giữ lại đường dẫn để quay về sau khi đăng nhập. */
function RequireAuth({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth()
  if (loading) return <Spinner />
  if (!user) return <Navigate to={`/dang-nhap?next=${encodeURIComponent(location.pathname)}`} replace />
  return <>{children}</>
}

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <ThemeProvider>
      <BrowserRouter>
        <AuthProvider>
          <Suspense fallback={<Spinner />}>
          <Routes>
            <Route element={<ClientLayout />}>
              <Route index element={<Home />} />
              <Route path="phim" element={<Movies />} />
              <Route path="phim/:id" element={<MovieDetail />} />
              <Route path="rap" element={<Cinemas />} />
              <Route path="uu-dai" element={<Promotions />} />
              <Route path="dang-nhap" element={<Login />} />
              <Route path="dang-ky" element={<Register />} />
              <Route path="dat-ve/:showtimeId" element={<Booking />} />
              <Route path="dat-ve/ket-qua/:bookingId"
                     element={<RequireAuth><BookingResult /></RequireAuth>} />
              <Route path="ve-cua-toi" element={<RequireAuth><MyTickets /></RequireAuth>} />
              <Route path="soat-ve" element={<RequireAuth><CheckIn /></RequireAuth>} />
            </Route>

            <Route path="/admin" element={<AdminLayout />}>
              <Route index element={<Dashboard />} />
              <Route path="phim" element={<AdminMovies />} />
              <Route path="suat-chieu" element={<AdminShowtimes />} />
              <Route path="rap" element={<AdminRooms />} />
              <Route path="khuyen-mai" element={<AdminPromotions />} />
              <Route path="bao-cao" element={<AdminReports />} />
            </Route>

            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
          </Suspense>
        </AuthProvider>
      </BrowserRouter>
      </ThemeProvider>
    </QueryClientProvider>
  )
}
