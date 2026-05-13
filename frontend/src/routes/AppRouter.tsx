import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { LoginPage } from '../features/auth/LoginPage'
import { SetupPage } from '../features/auth/SetupPage'
import { AppLayout } from '../components/AppLayout'
import { SettingsLayout } from '../features/settings/SettingsLayout'
import { GitSettings } from '../features/settings/integrations/GitSettings'
import { JiraSettings } from '../features/settings/integrations/JiraSettings'
import { ConfluenceSettings } from '../features/settings/integrations/ConfluenceSettings'
import { RepositoriesPage } from '../features/settings/repositories/RepositoriesPage'
import { ProjectsPage } from '../features/settings/projects/ProjectsPage'
import { ProtectedRoute } from './ProtectedRoute'
import { AdminRoute } from './AdminRoute'

function Dashboard() {
  return (
    <div className="p-8">
      <h1 className="text-2xl font-semibold">Dashboard</h1>
      <p className="text-gray-500 mt-2">Welcome to Repository Release Manager.</p>
    </div>
  )
}

export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/setup" element={<SetupPage />} />
        <Route path="/login" element={<LoginPage />} />

        <Route element={<ProtectedRoute />}>
          <Route element={<AppLayout />}>
            <Route path="/dashboard" element={<Dashboard />} />
            <Route path="/" element={<Navigate to="/dashboard" replace />} />

            {/* Settings — all sub-routes require authentication; write actions require Admin */}
            <Route path="/settings" element={<SettingsLayout />}>
              <Route index element={<Navigate to="integrations/git" replace />} />
              <Route path="repositories" element={<RepositoriesPage />} />
              <Route path="projects" element={<ProjectsPage />} />

              {/* Admin-only integration settings */}
              <Route element={<AdminRoute />}>
                <Route path="integrations/git" element={<GitSettings />} />
                <Route path="integrations/jira" element={<JiraSettings />} />
                <Route path="integrations/confluence" element={<ConfluenceSettings />} />
              </Route>
            </Route>

            <Route element={<AdminRoute />}>
              {/* Additional admin-only routes added in later phases */}
            </Route>
          </Route>
        </Route>

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
