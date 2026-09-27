import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { api, tokenStore } from './api'
import type { AuthResponse, Role, User } from './types'

interface AuthState {
  user: User | null
  loading: boolean
  login: (email: string, password: string) => Promise<User>
  register: (data: { email: string; password: string; fullName: string; phone?: string }) => Promise<User>
  logout: () => void
  hasRole: (...roles: Role[]) => boolean
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    if (!tokenStore.get()) { setLoading(false); return }
    api.get<User>('/api/auth/me')
      .then(setUser)
      .catch(() => tokenStore.clear())
      .finally(() => setLoading(false))
  }, [])

  const accept = (res: AuthResponse) => {
    tokenStore.set(res.accessToken)
    setUser(res.user)
    return res.user
  }

  const login = useCallback(async (email: string, password: string) =>
    accept(await api.post<AuthResponse>('/api/auth/login', { email, password })), [])

  const register = useCallback(async (data: {
    email: string; password: string; fullName: string; phone?: string
  }) => accept(await api.post<AuthResponse>('/api/auth/register',
    { ...data, phone: data.phone ?? null })), [])

  const logout = useCallback(() => { tokenStore.clear(); setUser(null) }, [])

  const value = useMemo<AuthState>(() => ({
    user, loading, login, register, logout,
    hasRole: (...roles) => !!user && roles.includes(user.role),
  }), [user, loading, login, register, logout])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const ctx = useContext(AuthContext)
  if (!ctx) throw new Error('useAuth phải nằm trong AuthProvider')
  return ctx
}
