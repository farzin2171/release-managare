import { create } from 'zustand'
import { persist } from 'zustand/middleware'

const BASE_URL = (import.meta.env.VITE_API_URL as string | undefined) || 'https://localhost:7140'

interface AuthState {
  accessToken: string | null
  role: 'Admin' | 'Viewer' | null
  isAuthenticated: boolean
  flashMessage: string | null
  setTokens: (accessToken: string) => void
  scheduleRefresh: (accessToken: string) => void
  clearAuth: () => void
  setFlashMessage: (msg: string | null) => void
}

let _refreshTimer: ReturnType<typeof setTimeout> | null = null

function decodeRole(token: string): 'Admin' | 'Viewer' | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    const role =
      payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] ?? payload['role']
    return role === 'Admin' || role === 'Viewer' ? role : null
  } catch {
    return null
  }
}

function decodeExp(token: string): number | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    return typeof payload.exp === 'number' ? payload.exp : null
  } catch {
    return null
  }
}

async function triggerSilentRefresh() {
  try {
    const res = await fetch(`${BASE_URL}/api/v1/auth/refresh`, {
      method: 'POST',
      credentials: 'include',
      headers: { 'Content-Type': 'application/json' },
    })
    if (!res.ok) {
      useAuthStore.getState().clearAuth()
      return
    }
    const { accessToken } = await res.json()
    useAuthStore.getState().setTokens(accessToken)
  } catch {
    useAuthStore.getState().clearAuth()
  }
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      role: null,
      isAuthenticated: false,
      flashMessage: null,

      setTokens: (accessToken) => {
        set({ accessToken, role: decodeRole(accessToken), isAuthenticated: true })
        useAuthStore.getState().scheduleRefresh(accessToken)
      },

      scheduleRefresh: (accessToken) => {
        if (_refreshTimer !== null) {
          clearTimeout(_refreshTimer)
          _refreshTimer = null
        }
        const exp = decodeExp(accessToken)
        if (exp === null) return
        const msUntilRefresh = exp * 1000 - Date.now() - 120_000
        if (msUntilRefresh > 0) {
          _refreshTimer = setTimeout(triggerSilentRefresh, msUntilRefresh)
        } else {
          void triggerSilentRefresh()
        }
      },

      clearAuth: () => {
        if (_refreshTimer !== null) {
          clearTimeout(_refreshTimer)
          _refreshTimer = null
        }
        set({ accessToken: null, role: null, isAuthenticated: false })
      },

      setFlashMessage: (msg) => set({ flashMessage: msg }),
    }),
    {
      name: 'auth-storage',
      partialize: (state) => ({
        accessToken: state.accessToken,
        role: state.role,
        isAuthenticated: state.isAuthenticated,
        flashMessage: state.flashMessage,
      }),
      onRehydrateStorage: () => (state) => {
        if (state?.accessToken) {
          state.scheduleRefresh(state.accessToken)
        }
      },
    }
  )
)
