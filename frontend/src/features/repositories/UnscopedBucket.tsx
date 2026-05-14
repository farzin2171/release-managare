import type { components } from '../../lib/api'

type CommitDto = components['schemas']['CommitDto']

interface UnscopedBucketProps {
  commits: CommitDto[]
}

export function UnscopedBucket({ commits }: UnscopedBucketProps) {
  if (commits.length === 0) return null

  return (
    <div className="rounded-lg border border-amber-200 dark:border-amber-700/50 overflow-hidden">
      <div className="flex items-center gap-2 px-4 py-2.5 bg-amber-50 dark:bg-amber-900/20 border-b border-amber-200 dark:border-amber-700/50">
        <svg className="w-4 h-4 text-amber-600 dark:text-amber-400 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z" />
        </svg>
        <span className="text-sm font-medium text-amber-800 dark:text-amber-300">
          Unscoped commits ({commits.length})
        </span>
        <span className="text-xs text-amber-600 dark:text-amber-500">
          — not linked to any Jira ticket; excluded from release notes
        </span>
      </div>
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-amber-100 dark:divide-amber-800/30 text-sm">
          <thead className="bg-amber-50/50 dark:bg-amber-900/10">
            <tr>
              {['SHA', 'Message', 'Author', 'Date'].map((h) => (
                <th
                  key={h}
                  className="px-4 py-2 text-left text-xs font-medium uppercase tracking-wider text-amber-700 dark:text-amber-400"
                >
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="bg-white dark:bg-gray-900 divide-y divide-amber-50 dark:divide-amber-900/20">
            {commits.map((c) => (
              <tr key={c.sha} className="hover:bg-amber-50/50 dark:hover:bg-amber-900/10">
                <td className="px-4 py-2 font-mono text-xs text-gray-500 dark:text-gray-400 whitespace-nowrap">
                  {c.shortSha}
                </td>
                <td className="px-4 py-2 text-gray-700 dark:text-gray-300 max-w-md truncate">
                  {c.message}
                </td>
                <td className="px-4 py-2 text-gray-500 dark:text-gray-400 whitespace-nowrap">
                  {c.author}
                </td>
                <td className="px-4 py-2 text-gray-500 dark:text-gray-400 whitespace-nowrap">
                  {new Date(c.committedAt).toLocaleDateString()}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}
