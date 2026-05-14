import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '../../lib/apiClient'
import { useAuthStore } from '../../lib/authStore'
import type { components } from '../../lib/api'

type ReconciliationResultDto = components['schemas']['ReconciliationResultDto']
type MatchedTicketDto = components['schemas']['MatchedTicketDto']
type JiraOnlyTicketDto = components['schemas']['JiraOnlyTicketDto']
type GitOnlyTicketDto = components['schemas']['GitOnlyTicketDto']

interface ReconciliationPanelProps {
  releaseId: string
  onBack: () => void
  onNext: () => void
}

function MatchRateBadge({ percent }: { percent: number }) {
  const color =
    percent >= 80 ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-400'
    : percent >= 50 ? 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900/30 dark:text-yellow-400'
    : 'bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400'
  return (
    <span className={`inline-flex items-center rounded-full px-3 py-1 text-sm font-semibold ${color}`}>
      {percent.toFixed(1)}% matched
    </span>
  )
}

function BucketSection({ title, count, children }: { title: string; count: number; children: React.ReactNode }) {
  const [open, setOpen] = useState(true)
  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
      <button
        onClick={() => setOpen((o) => !o)}
        className="w-full flex items-center justify-between px-4 py-3 bg-gray-50 dark:bg-gray-800 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors text-left"
      >
        <span className="text-sm font-medium text-gray-700 dark:text-gray-300">{title}</span>
        <span className="flex items-center gap-2">
          <span className="text-xs tabular-nums text-gray-500 dark:text-gray-400">{count}</span>
          <span className="text-gray-400 text-xs">{open ? '▲' : '▼'}</span>
        </span>
      </button>
      {open && <div className="divide-y divide-gray-100 dark:divide-gray-800">{children}</div>}
    </div>
  )
}

function MatchedRow({ ticket }: { ticket: MatchedTicketDto }) {
  return (
    <div className="flex items-center justify-between px-4 py-2.5 text-sm">
      <span className="font-mono text-xs font-medium text-gray-700 dark:text-gray-300">{ticket.key}</span>
      <span className="text-gray-600 dark:text-gray-400 truncate mx-3 flex-1">{ticket.summary}</span>
      <span className="text-xs rounded-full px-2 py-0.5 bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400 shrink-0">
        {ticket.status}
      </span>
    </div>
  )
}

function JiraOnlyRow({ ticket }: { ticket: JiraOnlyTicketDto }) {
  return (
    <div className="flex items-center px-4 py-2.5 text-sm gap-3">
      <span className="font-mono text-xs font-medium text-yellow-700 dark:text-yellow-400">{ticket.key}</span>
      <span className="text-gray-600 dark:text-gray-400 truncate flex-1">{ticket.summary}</span>
    </div>
  )
}

function GitOnlyRow({
  ticket,
  selected,
  onToggle,
}: {
  ticket: GitOnlyTicketDto
  selected: boolean
  onToggle: () => void
}) {
  return (
    <label className="flex items-center gap-3 px-4 py-2.5 text-sm cursor-pointer hover:bg-gray-50 dark:hover:bg-gray-800/50">
      <input
        type="checkbox"
        checked={selected}
        onChange={onToggle}
        className="accent-blue-600"
      />
      <span className="font-mono text-xs font-medium text-blue-700 dark:text-blue-400">{ticket.ticketId}</span>
      <span className="text-gray-600 dark:text-gray-400 truncate flex-1">{ticket.title}</span>
      <span className="text-xs text-gray-400 shrink-0">{ticket.commitCount} commit{ticket.commitCount !== 1 ? 's' : ''}</span>
    </label>
  )
}

export function ReconciliationPanel({ releaseId, onBack, onNext }: ReconciliationPanelProps) {
  const role = useAuthStore((s) => s.role)
  const isAdmin = role === 'Admin'
  const qc = useQueryClient()

  const [selectedGitOnly, setSelectedGitOnly] = useState<Set<string>>(new Set())

  const { data: result, isLoading } = useQuery<ReconciliationResultDto | null>({
    queryKey: ['reconciliation', releaseId],
    queryFn: () =>
      apiFetch(`/api/v1/releases/${releaseId}/reconciliation`).then((r) =>
        r.status === 404 ? null : r.json(),
      ),
    retry: false,
  })

  const reconcileMutation = useMutation({
    mutationFn: () =>
      apiFetch(`/api/v1/releases/${releaseId}/reconcile`, { method: 'POST' }).then((r) => {
        if (!r.ok) throw new Error('Reconciliation failed')
        return r.json() as Promise<ReconciliationResultDto>
      }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['reconciliation', releaseId] }),
  })

  const addToJiraMutation = useMutation({
    mutationFn: (ticketKeys: string[]) =>
      apiFetch(`/api/v1/releases/${releaseId}/reconciliation/jira-tickets`, {
        method: 'POST',
        body: JSON.stringify({ ticketKeys }),
      }).then((r) => {
        if (!r.ok) throw new Error('Failed to add tickets to Jira')
      }),
    onSuccess: () => {
      setSelectedGitOnly(new Set())
      qc.invalidateQueries({ queryKey: ['reconciliation', releaseId] })
    },
  })

  const toggleGitOnly = (key: string) => {
    setSelectedGitOnly((prev) => {
      const next = new Set(prev)
      if (next.has(key)) next.delete(key)
      else next.add(key)
      return next
    })
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-base font-semibold text-gray-900 dark:text-white">Step 4 — Jira reconciliation (optional)</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Compare commits in this release against Jira fix-version tickets. Skip if not needed.
        </p>
      </div>

      {/* Run reconciliation */}
      {!result && !isLoading && (
        <div className="rounded-lg border border-dashed border-gray-300 dark:border-gray-600 p-6 text-center">
          <p className="text-sm text-gray-500 dark:text-gray-400 mb-3">No reconciliation run yet.</p>
          <button
            onClick={() => reconcileMutation.mutate()}
            disabled={reconcileMutation.isPending}
            className="px-4 py-2 rounded-md bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            {reconcileMutation.isPending ? 'Running…' : 'Run reconciliation'}
          </button>
          {reconcileMutation.isError && (
            <p className="mt-2 text-xs text-red-500">Reconciliation failed. Check Jira connection settings.</p>
          )}
        </div>
      )}

      {isLoading && <p className="text-sm text-gray-500">Loading reconciliation data…</p>}

      {result && (
        <div className="space-y-4">
          {/* Summary row */}
          <div className="flex items-center gap-4 flex-wrap">
            <MatchRateBadge percent={result.matchRatePercent} />
            <span className="text-sm text-gray-500 dark:text-gray-400">
              {result.matchedCount} matched · {result.jiraOnlyCount} Jira-only · {result.gitOnlyCount} Git-only
            </span>
            <button
              onClick={() => reconcileMutation.mutate()}
              disabled={reconcileMutation.isPending}
              className="ml-auto text-xs text-blue-600 hover:underline disabled:opacity-40"
            >
              {reconcileMutation.isPending ? 'Running…' : 'Re-run'}
            </button>
          </div>

          {/* Matched */}
          {result.matched.length > 0 && (
            <BucketSection title="Matched" count={result.matched.length}>
              {result.matched.map((t) => <MatchedRow key={t.key} ticket={t} />)}
            </BucketSection>
          )}

          {/* Jira-only */}
          {result.jiraOnly.length > 0 && (
            <BucketSection title="Jira-only (in fix version, not in Git)" count={result.jiraOnly.length}>
              {result.jiraOnly.map((t) => <JiraOnlyRow key={t.key} ticket={t} />)}
            </BucketSection>
          )}

          {/* Git-only */}
          {result.gitOnly.length > 0 && (
            <BucketSection title="Git-only (in commits, not in Jira fix version)" count={result.gitOnly.length}>
              {result.gitOnly.map((t) => (
                <GitOnlyRow
                  key={t.ticketId}
                  ticket={t}
                  selected={selectedGitOnly.has(t.ticketId)}
                  onToggle={() => toggleGitOnly(t.ticketId)}
                />
              ))}
              {isAdmin && (
                <div className="px-4 py-3 bg-gray-50 dark:bg-gray-800 border-t border-gray-100 dark:border-gray-700">
                  <button
                    onClick={() => addToJiraMutation.mutate(Array.from(selectedGitOnly))}
                    disabled={selectedGitOnly.size === 0 || addToJiraMutation.isPending}
                    className="px-3 py-1.5 rounded-md bg-blue-600 text-white text-xs font-medium hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                  >
                    {addToJiraMutation.isPending
                      ? 'Adding…'
                      : `Add ${selectedGitOnly.size > 0 ? selectedGitOnly.size + ' ' : ''}selected to Jira fix version`}
                  </button>
                  {addToJiraMutation.isError && (
                    <span className="ml-3 text-xs text-red-500">Failed to add tickets.</span>
                  )}
                </div>
              )}
            </BucketSection>
          )}
        </div>
      )}

      <div className="flex justify-between">
        <button
          onClick={onBack}
          className="px-5 py-2 rounded-md border border-gray-300 dark:border-gray-600 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
        >
          Back
        </button>
        <button
          onClick={onNext}
          className="px-5 py-2 rounded-md bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 transition-colors"
        >
          Next: Publish
        </button>
      </div>
    </div>
  )
}
