const TOKEN_KEY = 'cinebook.token'

export const tokenStore = {
  get: () => { try { return localStorage.getItem(TOKEN_KEY) } catch { return null } },
  set: (t: string) => { try { localStorage.setItem(TOKEN_KEY, t) } catch { /* trình duyệt chặn */ } },
  clear: () => { try { localStorage.removeItem(TOKEN_KEY) } catch { /* trình duyệt chặn */ } },
}

export class ApiError extends Error {
  readonly status: number
  readonly code: string | null

  constructor(status: number, code: string | null, message: string) {
    super(message)
    this.status = status
    this.code = code
  }
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const token = tokenStore.get()
  if (token) headers.Authorization = `Bearer ${token}`

  const res = await fetch(path, {
    method,
    headers,
    body: body === undefined ? undefined : JSON.stringify(body),
  })

  if (res.status === 204) return undefined as T

  const text = await res.text()
  const data = text ? safeParse(text) : null

  if (!res.ok) {
    if (res.status === 401) tokenStore.clear()
    throw new ApiError(
      res.status,
      (data as { code?: string })?.code ?? null,
      (data as { message?: string })?.message ?? messageForStatus(res.status),
    )
  }
  return data as T
}

function safeParse(text: string): unknown {
  try { return JSON.parse(text) } catch { return text }
}

function messageForStatus(status: number) {
  if (status === 401) return 'Bạn cần đăng nhập để tiếp tục.'
  if (status === 403) return 'Bạn không có quyền thực hiện thao tác này.'
  if (status === 404) return 'Không tìm thấy dữ liệu.'
  if (status >= 500) return 'Hệ thống đang bận, vui lòng thử lại sau ít phút.'
  return 'Yêu cầu không thực hiện được.'
}

export const api = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  patch: <T>(path: string, body?: unknown) => request<T>('PATCH', path, body),
  del: <T>(path: string) => request<T>('DELETE', path),
}
