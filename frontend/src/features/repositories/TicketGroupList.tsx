import { useState } from 'react'
import type { components } from '../../lib/api'

type TicketGroupDto = components['schemas']['TicketGroupDto']
type CommitDto = components['schemas']['CommitDto']

const TYPE_COLOURS: Record<string, string> = {
  feat: 'bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300',
  fix: 'bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300',
  docs: 'bg-purple-100 text-purple-800 dark:bg-purple-900/40 dark:text-purple-300',
  refactor: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/40 dark:text-yellow-300',
  perf: 'bg-orange-100 text-orange-800 dark:bg-orange-900/40 dark:text-orange-300',
  test: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-900/40 dark:text-indigo-300',
  chore: 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300',
}

function typeBadgeClass(type: string) {
  return TYPE_COLOURS[type] ?? 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300'
}

function CommitRow({ commit }: { commit: CommitDto }) {
  return (
    <tr className="text-xs text-gray-600 dark:text-gray-400 bg-gray-50 dark:bg-gray-800/50">
      <td className="pl-10 pr-4 py-1.5 font-mono">{commit.shortSha}</td>
      <td className="pr-4 py-1.5 truncate max-w-xs">{commit.message}</td>
      <td className="pr-4 py-1.5">{commit.author}</td>
      <td className="pr-4 py-1.5 whitespace-nowrap">
        {new Date(commit.committedAt).toLocaleDateString()}
      </td>
    </tr>
  )
}

function TicketRow({ group }: { group: TicketGroupDto }) {
  const [expanded, setExpanded] = useState(false)

  return (
    <>
      <tr
        className="cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800/40 transition-colors"
        onClick={() => setExpanded((v) => !v)}
      >
        <td className="px-4 py-3 font-mono text-sm font-medium text-blue-600 dark:text-blue-400 whitespace-nowrap">
          <span className="flex items-center gap-1">
            <span className={`transition-transform text-xs text-gray-400 ${expanded ? 'rotate-90' : ''}`}>▶</span>
            {group.key}
          </span>
        </td>
        <td className="px-4 py-3 text-sm text-gray-900 dark:text-white">
          <span className="flex items-center gap-2">
            {group.title}
            {group.isBreaking && (
              <span className="inline-flex items-center rounded px-1.5 py-0.5 text-xs font-semibold bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-400">
                BREAKING
              </span>
            )}
          </span>
        </td>
        <td className="px-4 py-3">
          <span className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${typeBadgeClass(group.type)}`}>
            {group.type}
          </span>
        </td>
        <td className="px-4 py-3 text-sm text-gray-500 dark:text-gray-400 text-right tabular-nums">
          {group.commitCount}
        </td>
        <td className="px-4 py-3 text-sm text-gray-500 dark:text-gray-400 text-right tabular-nums">
          {group.contributorCount}
        </td>
      </tr>
      {expanded &&
        group.commits.map((c) => <CommitRow key={c.sha} commit={c} />)}
    </>
  )
}

interface TicketGroupListProps {
  groups: TicketGroupDto[]
  search: string
  typeFilter: string
}

export function TicketGroupList({ groups, search, typeFilter }: TicketGroupListProps) {
  const filtered = groups.filter((g) => {
    if (typeFilter && g.type !== typeFilter) return false
    if (search && !g.key.toLowerCase().includes(search.toLowerCase())) return false
    return true
  })

  if (filtered.length === 0) {
    return (
      <p className="text-sm text-gray-400 py-4">
        {search || typeFilter ? 'No ticket groups match your filters.' : 'No ticket groups found.'}
      </p>
    )
  }

  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-x-auto">
      <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
        <thead className="bg-gray-50 dark:bg-gray-800">
          <tr>
            {['Ticket', 'Title', 'Type', 'Commits', 'Contributors'].map((h) => (
              <th
                key={h}
                className={`px-4 py-3 text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 ${
                  h === 'Commits' || h === 'Contributors' ? 'text-right' : 'text-left'
                }`}
              >
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="bg-white dark:bg-gray-900 divide-y divide-gray-200 dark:divide-gray-700">
          {filtered.map((g) => (
            <TicketRow key={g.key} group={g} />
          ))}
        </tbody>
      </table>
    </div>
  )
}
