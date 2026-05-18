import { useState } from 'react'
import type { components } from '../../../lib/api'

type TicketSummaryDto = components['schemas']['TicketSummaryDto']

interface BucketListProps {
  inBoth: TicketSummaryDto[]
  jiraOnly: TicketSummaryDto[]
  gitOnly: TicketSummaryDto[]
  jiraBaseUrl?: string
}

const statusColors: Record<string, string> = {
  Done: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
  'In Progress': 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200',
  'To Do': 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300',
}

function StatusBadge({ status }: { status: string | null }) {
  if (!status) return null
  const colorClass = statusColors[status] ?? statusColors['To Do']
  return (
    <span className={`inline-flex rounded px-1.5 py-0.5 text-xs font-medium ${colorClass}`}>
      {status}
    </span>
  )
}

function TicketRow({
  ticket,
  jiraBaseUrl,
}: {
  ticket: TicketSummaryDto
  jiraBaseUrl?: string
}) {
  const ticketUrl = jiraBaseUrl ? `${jiraBaseUrl.replace(/\/$/, '')}/browse/${ticket.key}` : null

  return (
    <div className="flex items-center gap-3 py-2 text-sm border-b border-gray-100 dark:border-gray-800 last:border-0">
      {ticket.assigneeAvatarUrl ? (
        <img
          src={ticket.assigneeAvatarUrl}
          alt=""
          className="w-6 h-6 rounded-full shrink-0"
          loading="lazy"
        />
      ) : (
        <span className="w-6 h-6 rounded-full bg-gray-200 dark:bg-gray-700 shrink-0" />
      )}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          {ticketUrl ? (
            <a
              href={ticketUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="font-mono text-xs font-medium text-blue-600 dark:text-blue-400 hover:underline shrink-0"
            >
              {ticket.key}
            </a>
          ) : (
            <span className="font-mono text-xs font-medium text-gray-700 dark:text-gray-300 shrink-0">
              {ticket.key}
            </span>
          )}
          {ticket.summary && (
            <span className="text-gray-600 dark:text-gray-400 truncate">{ticket.summary}</span>
          )}
        </div>
      </div>
      <div className="flex items-center gap-2 shrink-0">
        <StatusBadge status={ticket.status} />
        {ticket.commitCount > 0 && (
          <span className="text-xs text-gray-400">{ticket.commitCount}c</span>
        )}
      </div>
    </div>
  )
}

interface BucketProps {
  title: string
  accent: string
  tickets: TicketSummaryDto[]
  defaultCollapsed?: boolean
  jiraBaseUrl?: string
}

function Bucket({ title, accent, tickets, defaultCollapsed = false, jiraBaseUrl }: BucketProps) {
  const [open, setOpen] = useState(!defaultCollapsed)

  if (tickets.length === 0) return null

  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900">
      <button
        type="button"
        className="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors rounded-lg"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
      >
        <div className="flex items-center gap-2">
          <span className={`inline-block w-2 h-2 rounded-full ${accent}`} />
          <span className="text-sm font-medium text-gray-900 dark:text-white">{title}</span>
          <span className="text-xs text-gray-400 font-tabular-nums">({tickets.length})</span>
        </div>
        <svg
          className={`w-4 h-4 text-gray-400 transition-transform ${open ? 'rotate-180' : ''}`}
          fill="none"
          viewBox="0 0 24 24"
          stroke="currentColor"
          strokeWidth={2}
        >
          <path strokeLinecap="round" strokeLinejoin="round" d="M19 9l-7 7-7-7" />
        </svg>
      </button>
      {open && (
        <div className="px-4 pb-3">
          {tickets.map((t) => (
            <TicketRow key={t.key} ticket={t} jiraBaseUrl={jiraBaseUrl} />
          ))}
        </div>
      )}
    </div>
  )
}

export function BucketList({ inBoth, jiraOnly, gitOnly, jiraBaseUrl }: BucketListProps) {
  const total = inBoth.length + jiraOnly.length + gitOnly.length
  if (total === 0) {
    return (
      <p className="text-sm text-gray-400 text-center py-4">No tickets to compare.</p>
    )
  }

  return (
    <div className="space-y-2">
      <Bucket
        title="In both"
        accent="bg-green-500"
        tickets={inBoth}
        defaultCollapsed={inBoth.length >= 5}
        jiraBaseUrl={jiraBaseUrl}
      />
      <Bucket
        title="Jira only"
        accent="bg-amber-500"
        tickets={jiraOnly}
        defaultCollapsed={false}
        jiraBaseUrl={jiraBaseUrl}
      />
      <Bucket
        title="Git only"
        accent="bg-red-500"
        tickets={gitOnly}
        defaultCollapsed={false}
        jiraBaseUrl={jiraBaseUrl}
      />
    </div>
  )
}
