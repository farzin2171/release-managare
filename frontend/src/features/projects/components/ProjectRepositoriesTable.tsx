import { LatestTagCell } from '../../repositories/components/LatestTagCell'
import type { components } from '../../../lib/api'

type RepositoryDto = components['schemas']['RepositoryDto']

interface Props {
  repositories: RepositoryDto[]
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
