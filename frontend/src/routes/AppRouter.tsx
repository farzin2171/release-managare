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
import { TemplatesPage } from '../features/settings/templates/TemplatesPage'
import { UsersPage } from '../features/settings/users/UsersPage'
import { ProjectsListPage } from '../features/projects/ProjectsListPage'
import { ProjectDashboard } from '../features/projects/ProjectDashboard'
import { RepositoryDetail } from '../features/repositories/RepositoryDetail'
import { ReleaseWizard } from '../features/releases/wizard/ReleaseWizard'
import { ReleaseDetail } from '../features/releases/ReleaseDetail'
import { ProtectedRoute } from './ProtectedRoute'
import { AdminRoute } from './AdminRoute'

export function AppRouter() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/setup" element={<SetupPage />} />
        <Route path="/login" element={<LoginPage />} />

        <Route element={<ProtectedRoute />}>
          <Route element={<AppLayout />}>
            <Route path="/" element={<Navigate to="/projects" replace />} />
            <Route path="/dashboard" element={<Navigate to="/projects" replace />} />

            {/* Projects (viewer + admin) */}
            <Route path="/projects" element={<ProjectsListPage />} />
            <Route path="/projects/:id" element={<ProjectDashboard />} />

            {/* Repository detail */}
            <Route path="/repositories/:id" element={<RepositoryDetail />} />

            {/* Releases */}
            <Route path="/projects/:id/releases/new" element={<ReleaseWizard />} />
            <Route path="/releases/:id" element={<ReleaseDetail />} />

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
                <Route path="templates" element={<TemplatesPage />} />
                <Route path="users" element={<UsersPage />} />
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
