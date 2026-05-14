import type { components } from '../../lib/api'

type CommitDto = components['schemas']['CommitDto']
type TicketGroupDto = components['schemas']['TicketGroupDto']

interface CommitsViewProps {
  groups: TicketGroupDto[]
  unscoped: CommitDto[]
  typeFilter: string
  search: string
}

export function CommitsView({ groups, unscoped, typeFilter, search }: CommitsViewProps) {
  const allCommits: (CommitDto & { ticketKey?: string; type?: string })[] = [
    ...groups
      .filter((g) => !typeFilter || g.type === typeFilter)
      .filter((g) => !search || g.key.toLowerCase().includes(search.toLowerCase()))
      .flatMap((g) => g.commits.map((c) => ({ ...c, ticketKey: g.key, type: g.type }))),
    ...(!typeFilter && !search ? unscoped : []),
  ].sort((a, b) => new Date(b.committedAt).getTime() - new Date(a.committedAt).getTime())

  if (allCommits.length === 0) {
    return <p className="text-sm text-gray-400 py-4">No commits match your filters.</p>
  }

  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-x-auto">
      <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
        <thead className="bg-gray-50 dark:bg-gray-800">
          <tr>
            {['SHA', 'Message', 'Ticket', 'Author', 'Date'].map((h) => (
              <th
                key={h}
                className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
              >
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="bg-white dark:bg-gray-900 divide-y divide-gray-200 dark:divide-gray-700">
          {allCommits.map((c) => (
            <tr key={c.sha} className="hover:bg-gray-50 dark:hover:bg-gray-800/40">
              <td className="px-4 py-2.5 font-mono text-xs text-gray-500 dark:text-gray-400 whitespace-nowrap">
                {c.shortSha}
              </td>
              <td className="px-4 py-2.5 text-gray-900 dark:text-white max-w-xs truncate">
                {c.message}
              </td>
              <td className="px-4 py-2.5 font-mono text-xs text-blue-600 dark:text-blue-400 whitespace-nowrap">
                {c.ticketKey ?? <span className="text-gray-400">—</span>}
              </td>
              <td className="px-4 py-2.5 text-gray-500 dark:text-gray-400 whitespace-nowrap">
                {c.author}
              </td>
              <td className="px-4 py-2.5 text-gray-500 dark:text-gray-400 whitespace-nowrap">
                {new Date(c.committedAt).toLocaleDateString()}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
