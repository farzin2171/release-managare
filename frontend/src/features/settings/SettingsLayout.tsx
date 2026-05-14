import { NavLink, Outlet } from 'react-router-dom'

const navGroups = [
  {
    label: 'Integrations',
    items: [
      { label: 'Azure DevOps', path: '/settings/integrations/git' },
      { label: 'Jira', path: '/settings/integrations/jira' },
      { label: 'Confluence', path: '/settings/integrations/confluence' },
    ],
  },
  {
    label: null,
    items: [
      { label: 'Repositories', path: '/settings/repositories' },
      { label: 'Projects', path: '/settings/projects' },
      { label: 'Templates', path: '/settings/templates' },
      { label: 'Users', path: '/settings/users' },
    ],
  },
]

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `block rounded-md px-3 py-2 text-sm font-medium transition-colors ${
    isActive
      ? 'bg-blue-50 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300'
      : 'text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700'
  }`

export function SettingsLayout() {
  return (
    <div className="flex min-h-screen bg-gray-50 dark:bg-gray-900">
      <aside className="w-56 shrink-0 border-r border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 p-4 space-y-6">
        <div>
          <p className="text-xs font-semibold uppercase tracking-wider text-gray-400 dark:text-gray-500 px-3 mb-2">
            Settings
          </p>
        </div>
        {navGroups.map((group, gi) => (
          <div key={gi}>
            {group.label && (
              <p className="text-xs font-semibold uppercase tracking-wider text-gray-400 dark:text-gray-500 px-3 mb-1">
                {group.label}
              </p>
            )}
            <nav className="space-y-0.5">
              {group.items.map((item) => (
                <NavLink key={item.path} to={item.path} className={linkClass}>
                  {item.label}
                </NavLink>
              ))}
            </nav>
          </div>
        ))}
      </aside>
      <main className="flex-1 p-8 overflow-auto">
        <Outlet />
      </main>
    </div>
  )
}
