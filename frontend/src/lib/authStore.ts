import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface AuthState {
  accessToken: string | null
  refreshToken: string | null
  role: 'Admin' | 'Viewer' | null
  isAuthenticated: boolean
  setTokens: (accessToken: string, refreshToken: string) => void
  clearAuth: () => void
}

function decodeRole(token: string): 'Admin' | 'Viewer' | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    const role = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
      ?? payload['role']
    return role === 'Admin' || role === 'Viewer' ? role : null
  } catch {
    return null
  }
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      refreshToken: null,
      role: null,
      isAuthenticated: false,
      setTokens: (accessToken, refreshToken) =>
        set({
          accessToken,
          refreshToken,
          role: decodeRole(accessToken),
          isAuthenticated: true,
        }),
      clearAuth: () =>
        set({ accessToken: null, refreshToken: null, role: null, isAuthenticated: false }),
    }),
    { name: 'auth-storage' }
  )
)
