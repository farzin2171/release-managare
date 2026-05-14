import { useState } from 'react'
import type { components } from '../../lib/api'

type CommitDto = components['schemas']['CommitDto']
type TicketGroupDto = components['schemas']['TicketGroupDto']

interface ContributorsViewProps {
  groups: TicketGroupDto[]
  unscoped: CommitDto[]
}

export function ContributorsView({ groups, unscoped }: ContributorsViewProps) {
  const [expandedAuthor, setExpandedAuthor] = useState<string | null>(null)

  const allCommits = [
    ...groups.flatMap((g) => g.commits),
    ...unscoped,
  ]

  const byAuthor = new Map<string, CommitDto[]>()
  for (const c of allCommits) {
    const list = byAuthor.get(c.author) ?? []
    list.push(c)
    byAuthor.set(c.author, list)
  }

  const authorEntries = [...byAuthor.entries()].sort((a, b) => b[1].length - a[1].length)

  if (authorEntries.length === 0) {
    return <p className="text-sm text-gray-400 py-4">No contributors found.</p>
  }

  return (
    <div className="space-y-2">
      {authorEntries.map(([author, commits]) => (
        <div
          key={author}
          className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden"
        >
          <button
            className="w-full flex items-center justify-between px-4 py-3 bg-white dark:bg-gray-900 hover:bg-gray-50 dark:hover:bg-gray-800/40 transition-colors"
            onClick={() => setExpandedAuthor(expandedAuthor === author ? null : author)}
          >
            <span className="flex items-center gap-3">
              <span
                className={`text-xs transition-transform text-gray-400 ${expandedAuthor === author ? 'rotate-90' : ''}`}
              >
                ▶
              </span>
              <span className="text-sm font-medium text-gray-900 dark:text-white">{author}</span>
            </span>
            <span className="text-sm text-gray-500 dark:text-gray-400 tabular-nums">
              {commits.length} {commits.length === 1 ? 'commit' : 'commits'}
            </span>
          </button>
          {expandedAuthor === author && (
            <div className="border-t border-gray-200 dark:border-gray-700 overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-100 dark:divide-gray-700/50 text-xs">
                <tbody className="bg-gray-50 dark:bg-gray-800/50">
                  {commits
                    .slice()
                    .sort((a, b) => new Date(b.committedAt).getTime() - new Date(a.committedAt).getTime())
                    .map((c) => (
                      <tr key={c.sha} className="hover:bg-gray-100 dark:hover:bg-gray-700/40">
                        <td className="pl-10 pr-4 py-1.5 font-mono text-gray-500 dark:text-gray-400 whitespace-nowrap">
                          {c.shortSha}
                        </td>
                        <td className="pr-4 py-1.5 text-gray-700 dark:text-gray-300 max-w-xs truncate">
                          {c.message}
                        </td>
                        <td className="pr-4 py-1.5 text-gray-500 dark:text-gray-400 whitespace-nowrap">
                          {new Date(c.committedAt).toLocaleDateString()}
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      ))}
    </div>
  )
}
