import { useAuthStore } from './authStore'

const BASE_URL = (import.meta.env.VITE_API_URL as string | undefined) || 'https://localhost:7140'

let refreshPromise: Promise<string> | null = null

async function doRefresh(): Promise<string> {
  const res = await fetch(`${BASE_URL}/api/v1/auth/refresh`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
  })
  if (!res.ok) throw new Error('refresh_failed')
  const { accessToken } = (await res.json()) as { accessToken: string }
  useAuthStore.getState().setTokens(accessToken)
  return accessToken
}

function buildHeaders(token: string | null, extra?: Record<string, string>): Record<string, string> {
  return {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...extra,
  }
}

export async function apiFetch(
  path: string,
  options?: RequestInit,
  _retried = false
): Promise<Response> {
  const token = useAuthStore.getState().accessToken
  const res = await fetch(`${BASE_URL}${path}`, {
    ...options,
    credentials: 'include',
    headers: buildHeaders(token, options?.headers as Record<string, string> | undefined),
  })

  // 401 on a non-refresh endpoint that hasn't already been retried → attempt silent refresh
  if (res.status === 401 && !_retried && !path.includes('/auth/refresh')) {
    try {
      if (refreshPromise === null) {
        refreshPromise = doRefresh().finally(() => {
          refreshPromise = null
        })
      }
      await refreshPromise
      return apiFetch(path, options, true)
    } catch {
      useAuthStore.getState().setFlashMessage('Your session has expired. Please log in again.')
      useAuthStore.getState().clearAuth()
      window.location.href = '/login'
    }
  }

  return res
}
