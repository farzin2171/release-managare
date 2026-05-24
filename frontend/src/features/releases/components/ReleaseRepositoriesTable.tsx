import type { components } from '../../../lib/api'

type ReleaseRepositoryDto = components['schemas']['ReleaseRepositoryDto']

const BUMP_COLORS: Record<string, string> = {
  major: 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400',
  minor: 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400',
  patch: 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300',
  manual: 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400',
}

function BumpTypeBadge({ type }: { type: string }) {
  const cls = BUMP_COLORS[type.toLowerCase()] ?? BUMP_COLORS['manual']
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${cls}`}>
      {type}
    </span>
  )
}

const EM_DASH = '—'

interface Props {
  rows: ReleaseRepositoryDto[]
}

export function ReleaseRepositoriesTable({ rows }: Props) {
  if (rows.length === 0) return null

  return (
    <div>
      <h2 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Repository versions</h2>
      <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
        <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
          <thead className="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Repository</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Previous</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Next</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Bump</th>
              <th className="px-4 py-2.5 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Commits</th>
              <th className="px-4 py-2.5 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Tickets</th>
            </tr>
          </thead>
          <tbody className="bg-white dark:bg-gray-900 divide-y divide-gray-100 dark:divide-gray-800">
            {rows.map((row) => (
              <tr key={row.id}>
                <td className="px-4 py-3">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-medium text-gray-900 dark:text-white">{row.repositoryName}</span>
                    {row.isLegacy && (
                      <span className="inline-flex items-center rounded px-1.5 py-0.5 text-xs font-medium bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400 border border-amber-200 dark:border-amber-800">
                        Pre-feature release — partial data
                      </span>
                    )}
                  </div>
                </td>
                <td className="px-4 py-3 font-mono text-xs text-gray-500 dark:text-gray-400">
                  {row.isLegacy || !row.previousVersion ? EM_DASH : row.previousVersion}
                </td>
                <td className="px-4 py-3 font-mono text-xs text-gray-700 dark:text-gray-300">
                  {row.isLegacy || !row.nextVersion ? EM_DASH : row.nextVersion}
                </td>
                <td className="px-4 py-3">
                  {row.isLegacy ? (
                    <span className="text-xs text-gray-400">{EM_DASH}</span>
                  ) : (
                    <BumpTypeBadge type={row.bumpType} />
                  )}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-gray-700 dark:text-gray-300">
                  {row.isLegacy ? EM_DASH : row.commitCount}
                </td>
                <td className="px-4 py-3 text-right tabular-nums text-gray-700 dark:text-gray-300">
                  {row.isLegacy ? EM_DASH : row.ticketCount}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
