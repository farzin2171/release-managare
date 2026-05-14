import { useAuthStore } from './authStore'

const BASE_URL = (import.meta.env.VITE_API_URL as string | undefined) || 'https://localhost:7140'

export async function apiFetch(path: string, options?: RequestInit): Promise<Response> {
  const token = useAuthStore.getState().accessToken
  return fetch(`${BASE_URL}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(options?.headers as Record<string, string> | undefined),
    },
  })
}
