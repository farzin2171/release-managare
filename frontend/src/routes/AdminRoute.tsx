import { Navigate, Outlet } from 'react-router-dom'
import { useAuthStore } from '../lib/authStore'

export function AdminRoute() {
  const role = useAuthStore((s) => s.role)

  if (role === null) return <Navigate to="/login" replace />
  if (role !== 'Admin') {
    return (
      <div className="min-h-screen flex items-center justify-center bg-gray-50 dark:bg-gray-900">
        <div className="text-center space-y-2">
          <h1 className="text-2xl font-semibold text-gray-900 dark:text-white">403 Forbidden</h1>
          <p className="text-sm text-gray-500 dark:text-gray-400">
            You do not have permission to access this page.
          </p>
        </div>
      </div>
    )
  }

  return <Outlet />
}
