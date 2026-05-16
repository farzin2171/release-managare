import type { components } from '../../../lib/api'

type RepositoryDto = components['schemas']['RepositoryDto']

interface Props {
  repositories: RepositoryDto[]
}

function LatestTagCell({ repo }: { repo: RepositoryDto }) {
  if (!repo.latestTag) {
    return (
      <div className="flex items-center gap-1.5">
        <span
          data-testid="amber-dot"
          className="h-2 w-2 rounded-full bg-amber-400 shrink-0"
        />
        <span className="text-gray-400 dark:text-gray-500 text-sm">—</span>
      </div>
    )
  }

  const shortSha = repo.latestTagCommitSha ? repo.latestTagCommitSha.slice(0, 7) : ''
  const date = repo.latestTagSetAt ? new Date(repo.latestTagSetAt).toLocaleDateString() : ''
  const email = repo.latestTagSetBy?.email ?? 'Unknown user'

  return (
    <div className="relative group inline-flex">
      <span
        data-testid="latest-tag-badge"
        className="inline-flex items-center font-mono text-xs border border-gray-300 dark:border-gray-600 rounded px-1.5 py-0.5 bg-white dark:bg-gray-800 text-gray-900 dark:text-white cursor-default select-none"
      >
        {repo.latestTag}
      </span>
      <div
        data-testid="tag-tooltip"
        className="absolute bottom-full left-0 mb-1.5 invisible group-hover:visible z-10 pointer-events-none"
      >
        <div className="bg-gray-900 text-white text-xs rounded py-1.5 px-2.5 whitespace-nowrap shadow-lg space-y-0.5">
          {shortSha && <div className="font-mono">{shortSha}</div>}
          {date && <div>{date}</div>}
          <div>{email}</div>
        </div>
      </div>
    </div>
  )
}

export function ProjectRepositoriesTable({ repositories }: Props) {
  if (repositories.length === 0) {
    return (
      <p className="text-sm text-gray-500 dark:text-gray-400 italic">
        No repositories assigned to this project.
      </p>
    )
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-gray-200 dark:border-gray-700">
            <th className="text-left pb-2 pr-6 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Repository
            </th>
            <th className="text-left pb-2 pr-6 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Branch
            </th>
            <th className="text-left pb-2 text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider">
              Latest tag
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
          {repositories.map((repo) => (
            <tr key={repo.id}>
              <td className="py-2.5 pr-6 font-medium text-gray-900 dark:text-white">
                {repo.name}
              </td>
              <td className="py-2.5 pr-6 font-mono text-xs text-gray-500 dark:text-gray-400">
                {repo.defaultBranch}
              </td>
              <td className="py-2.5">
                <LatestTagCell repo={repo} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
