import { useAuthStore } from '../../../lib/authStore'
import { LatestTagCell } from '../../repositories/components/LatestTagCell'
import type { components } from '../../../lib/api'

type RepositoryDto = components['schemas']['RepositoryDto']

interface RepositoriesTableProps {
  repos: RepositoryDto[]
  togglingId: string | null
  onToggleTracked: (repo: RepositoryDto) => void
  onRowClick: (repo: RepositoryDto) => void
}

export function RepositoriesTable({ repos, togglingId, onToggleTracked, onRowClick }: RepositoriesTableProps) {
  const isAdmin = useAuthStore((s) => s.role === 'Admin')

  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-x-auto">
      <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
        <thead className="bg-gray-50 dark:bg-gray-800">
          <tr>
            {['Repository', 'Service Owner', 'Azure project', 'Default branch', 'Web URL', 'Latest tag', 'Tracked'].map(
              (h) => (
                <th
                  key={h}
                  className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
                >
                  {h}
                </th>
              ),
            )}
          </tr>
        </thead>
        <tbody className="bg-white dark:bg-gray-900 divide-y divide-gray-200 dark:divide-gray-700">
          {repos.map((repo) => (
            <tr
              key={repo.id}
              onClick={() => onRowClick(repo)}
              className="hover:bg-gray-50 dark:hover:bg-gray-800/50 cursor-pointer"
            >
              <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">
                {repo.name}
              </td>
              <td className="px-4 py-3 text-gray-500 dark:text-gray-400">
                {repo.serviceOwner ?? '—'}
              </td>
              <td className="px-4 py-3 text-gray-500 dark:text-gray-400">
                {repo.azureProjectName}
              </td>
              <td className="px-4 py-3 text-gray-500 dark:text-gray-400 font-mono text-xs">
                {repo.defaultBranch}
              </td>
              <td className="px-4 py-3">
                <a
                  href={repo.webUrl}
                  target="_blank"
                  rel="noreferrer"
                  onClick={(e) => e.stopPropagation()}
                  className="text-blue-600 hover:underline dark:text-blue-400 truncate block max-w-xs"
                >
                  {repo.webUrl}
                </a>
              </td>
              <td className="px-4 py-3">
                <LatestTagCell repo={repo} />
              </td>
              <td className="px-4 py-3" onClick={(e) => e.stopPropagation()}>
                <button
                  role="switch"
                  aria-checked={repo.isTracked}
                  aria-label={`${repo.isTracked ? 'Untrack' : 'Track'} ${repo.name}`}
                  disabled={!isAdmin || togglingId === repo.id}
                  title={!isAdmin ? 'Admin access required' : undefined}
                  onClick={() => isAdmin && onToggleTracked(repo)}
                  className={`relative inline-flex h-5 w-9 shrink-0 rounded-full border-2 border-transparent transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50 ${
                    isAdmin ? 'cursor-pointer' : 'cursor-not-allowed'
                  } ${
                    repo.isTracked
                      ? 'bg-blue-600'
                      : 'bg-gray-200 dark:bg-gray-600'
                  }`}
                >
                  <span
                    className={`pointer-events-none inline-block h-4 w-4 transform rounded-full bg-white shadow transition-transform ${
                      repo.isTracked ? 'translate-x-4' : 'translate-x-0'
                    }`}
                  />
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
