import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '../../lib/apiClient'
import type { components } from '../../lib/api'

type ProjectDto = components['schemas']['ProjectDto']

export function ProjectsListPage() {
  const { data: projects = [], isLoading } = useQuery<ProjectDto[]>({
    queryKey: ['projects'],
    queryFn: () => apiFetch('/api/v1/projects').then((r) => r.json()),
  })

  if (isLoading) {
    return (
      <div className="p-8">
        <p className="text-sm text-gray-500">Loading projects…</p>
      </div>
    )
  }

  return (
    <div className="max-w-4xl p-6 space-y-6">
      <h1 className="text-xl font-semibold text-gray-900 dark:text-white">Projects</h1>

      {projects.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 dark:border-gray-600 p-12 text-center">
          <p className="text-sm text-gray-500 dark:text-gray-400">
            No projects yet. Create one in{' '}
            <Link to="/settings/projects" className="text-blue-600 dark:text-blue-400 hover:underline">
              Settings → Projects
            </Link>.
          </p>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
          {projects.map((p) => (
            <Link
              key={p.id}
              to={`/projects/${p.id}`}
              className="block rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 hover:shadow-md hover:border-blue-300 dark:hover:border-blue-600 transition-all"
            >
              <div className="flex items-center gap-2 mb-2">
                <span
                  className="inline-block w-3 h-3 rounded-full shrink-0"
                  style={{ backgroundColor: p.color }}
                />
                <span className="text-sm font-semibold text-gray-900 dark:text-white truncate">
                  {p.name}
                </span>
              </div>
              {p.description && (
                <p className="text-xs text-gray-500 dark:text-gray-400 line-clamp-2">
                  {p.description}
                </p>
              )}
              <p className="mt-3 text-xs text-blue-600 dark:text-blue-400 font-medium">
                View changes →
              </p>
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}
