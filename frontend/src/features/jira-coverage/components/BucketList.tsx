import { useState } from 'react'
import type { components } from '../../../lib/api'
import { useAddTicketToFixVersion } from '../hooks/useJiraCoverage'

type TicketSummaryDto = components['schemas']['TicketSummaryDto']

interface BucketListProps {
  inBoth: TicketSummaryDto[]
  jiraOnly: TicketSummaryDto[]
  gitOnly: TicketSummaryDto[]
  jiraBaseUrl?: string
  repositoryId?: string
  fixVersionName?: string
  isAdmin?: boolean
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
  onAddToFixVersion,
  addDisabled,
}: {
  ticket: TicketSummaryDto
  jiraBaseUrl?: string
  onAddToFixVersion?: (ticketKey: string) => void
  addDisabled?: boolean
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
        {onAddToFixVersion && (
          <button
            type="button"
            disabled={addDisabled}
            onClick={() => onAddToFixVersion(ticket.key)}
            className="text-xs text-blue-600 dark:text-blue-400 hover:underline disabled:opacity-40 disabled:cursor-not-allowed whitespace-nowrap"
          >
            + Fix version
          </button>
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
  onAddToFixVersion?: (ticketKey: string) => void
  addDisabled?: boolean
}

function Bucket({ title, accent, tickets, defaultCollapsed = false, jiraBaseUrl, onAddToFixVersion, addDisabled }: BucketProps) {
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
            <TicketRow
              key={t.key}
              ticket={t}
              jiraBaseUrl={jiraBaseUrl}
              onAddToFixVersion={onAddToFixVersion}
              addDisabled={addDisabled}
            />
          ))}
        </div>
      )}
    </div>
  )
}

function AddTicketDialog({
  ticketKey,
  fixVersionName,
  onConfirm,
  onCancel,
  isPending,
}: {
  ticketKey: string
  fixVersionName: string
  onConfirm: () => void
  onCancel: () => void
  isPending: boolean
}) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div className="absolute inset-0 bg-black/50" onClick={onCancel} />
      <div className="relative z-10 bg-white dark:bg-gray-900 rounded-lg shadow-xl p-6 max-w-sm w-full mx-4">
        <h2 className="text-base font-semibold text-gray-900 dark:text-white">Add to fix version?</h2>
        <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Ticket{' '}
          <span className="font-mono font-medium text-gray-900 dark:text-white">{ticketKey}</span>{' '}
          will be added to fix version{' '}
          <span className="font-mono font-medium text-gray-900 dark:text-white">{fixVersionName}</span>.
          The fix version will be created in Jira if it does not already exist.
        </p>
        <div className="mt-5 flex justify-end gap-2">
          <button
            type="button"
            onClick={onCancel}
            disabled={isPending}
            className="px-3 py-1.5 rounded-md text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 disabled:opacity-50"
          >
            Cancel
          </button>
          <button
            type="button"
            disabled={isPending}
            onClick={onConfirm}
            className="px-3 py-1.5 rounded-md text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50"
          >
            {isPending ? 'Adding…' : 'Confirm'}
          </button>
        </div>
      </div>
    </div>
  )
}

export function BucketList({ inBoth, jiraOnly, gitOnly, jiraBaseUrl, repositoryId, fixVersionName, isAdmin }: BucketListProps) {
  const [confirmingTicket, setConfirmingTicket] = useState<string | null>(null)
  const addTicketMutation = useAddTicketToFixVersion(repositoryId ?? '')

  const total = inBoth.length + jiraOnly.length + gitOnly.length
  if (total === 0) {
    return (
      <p className="text-sm text-gray-400 text-center py-4">No tickets to compare.</p>
    )
  }

  const canAddToFixVersion = isAdmin && !!repositoryId && !!fixVersionName

  const handleAddToFixVersion = (ticketKey: string) => {
    setConfirmingTicket(ticketKey)
  }

  const handleConfirm = () => {
    if (!confirmingTicket) return
    addTicketMutation.mutate(confirmingTicket, {
      onSuccess: () => setConfirmingTicket(null),
    })
  }

  return (
    <>
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
          onAddToFixVersion={canAddToFixVersion ? handleAddToFixVersion : undefined}
          addDisabled={addTicketMutation.isPending}
        />
      </div>

      {confirmingTicket && fixVersionName && (
        <AddTicketDialog
          ticketKey={confirmingTicket}
          fixVersionName={fixVersionName}
          onConfirm={handleConfirm}
          onCancel={() => setConfirmingTicket(null)}
          isPending={addTicketMutation.isPending}
        />
      )}
    </>
  )
}
