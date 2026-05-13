import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { useAuthStore } from '../lib/authStore'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `text-sm font-medium px-3 py-2 rounded-md transition-colors ${
    isActive
      ? 'bg-white/10 text-white'
      : 'text-white/70 hover:text-white hover:bg-white/10'
  }`

export function AppLayout() {
  const navigate = useNavigate()
  const { role, clearAuth } = useAuthStore()

  const handleLogout = () => {
    clearAuth()
    navigate('/login')
  }

  return (
    <div className="min-h-screen flex flex-col bg-gray-50 dark:bg-gray-900">
      <header className="bg-blue-700 dark:bg-gray-800 border-b border-blue-800 dark:border-gray-700 px-4 h-12 flex items-center gap-4 shrink-0">
        <NavLink
          to="/dashboard"
          className="text-white font-semibold text-sm mr-4 hover:text-white/80"
        >
          Release Manager
        </NavLink>
        <nav className="flex items-center gap-1">
          <NavLink to="/dashboard" className={linkClass}>
            Dashboard
          </NavLink>
          <NavLink to="/settings" className={linkClass}>
            Settings
          </NavLink>
        </nav>
        <div className="ml-auto flex items-center gap-3">
          {role && (
            <span className="text-xs font-medium px-2 py-0.5 rounded-full bg-white/20 text-white">
              {role}
            </span>
          )}
          <button
            onClick={handleLogout}
            className="text-sm text-white/70 hover:text-white transition-colors"
          >
            Logout
          </button>
        </div>
      </header>
      <div className="flex-1 min-h-0">
        <Outlet />
      </div>
    </div>
  )
}
