import { createContext, useCallback, useContext, useEffect, useState } from 'react'

export type Theme = 'light' | 'dark'

const KEY = 'cinebook.theme'

/** Đọc lựa chọn đã lưu; chưa chọn bao giờ thì theo nền tối như trong phòng chiếu. */
export function readTheme(): Theme {
  try {
    const saved = localStorage.getItem(KEY)
    if (saved === 'light' || saved === 'dark') return saved
  } catch { /* trình duyệt chặn localStorage */ }
  return 'dark'
}

function apply(theme: Theme) {
  const root = document.documentElement
  root.dataset.theme = theme
  // Bật hiệu ứng chuyển màu trong chốc lát rồi tắt, để lúc bình thường
  // các thao tác khác không bị dính transition thừa.
  root.classList.add('theme-switching')
  window.setTimeout(() => root.classList.remove('theme-switching'), 260)
  try { localStorage.setItem(KEY, theme) } catch { /* bỏ qua */ }
}

const ThemeContext = createContext<{ theme: Theme; toggle: () => void }>({
  theme: 'dark',
  toggle: () => {},
})

export function ThemeProvider({ children }: { children: React.ReactNode }) {
  const [theme, setTheme] = useState<Theme>(readTheme)

  // Đồng bộ khi người dùng đổi chủ đề ở tab khác.
  useEffect(() => {
    const onStorage = (e: StorageEvent) => {
      if (e.key !== KEY) return
      const next = readTheme()
      setTheme(next)
      document.documentElement.dataset.theme = next
    }
    window.addEventListener('storage', onStorage)
    return () => window.removeEventListener('storage', onStorage)
  }, [])

  const toggle = useCallback(() => {
    setTheme((current) => {
      const next: Theme = current === 'dark' ? 'light' : 'dark'
      apply(next)
      return next
    })
  }, [])

  return <ThemeContext value={{ theme, toggle }}>{children}</ThemeContext>
}

export const useTheme = () => useContext(ThemeContext)
