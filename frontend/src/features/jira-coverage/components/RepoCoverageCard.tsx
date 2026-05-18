import { Link } from 'react-router-dom'
import { HealthPill } from './HealthPill'
import { useRepoCoverage } from '../hooks/useJiraCoverage'
import { useAuthStore } from '../../../lib/authStore'
import type { components } from '../../../lib/api'

type RepoJiraComparisonDto = components['schemas']['RepoJiraComparisonDto']

interface RepoCoverageCardProps {
  coverage: RepoJiraComparisonDto
}

function formatRelative(isoDate: string): string {
  const date = new Date(isoDate)
  if (isNaN(date.getTime()) || date.getTime() === new Date(0).getTime()) return 'Never'
  const diffMs = Date.now() - date.getTime()
  const mins = Math.floor(diffMs / 60_000)
  if (mins < 1) return 'Just now'
  if (mins < 60) return `${mins}m ago`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.floor(hours / 24)}d ago`
}

function CounterBadge({ label, value }: { label: string; value: number }) {
  return (
    <div className="text-center">
      <p className="text-lg font-bold tabular-nums text-gray-900 dark:text-white">{value}</p>
      <p className="text-xs text-gray-500 dark:text-gray-400">{label}</p>
    </div>
  )
}

interface ResyncButtonProps {
  repoId: string
  lastSyncedAt: string
}

function ResyncButton({ repoId, lastSyncedAt }: ResyncButtonProps) {
  const isAdmin = useAuthStore((s) => s.role === 'Admin')
  const { refetch, isFetching } = useRepoCoverage(repoId, true)

  if (!isAdmin) return null

  return (
    <button
      type="button"
      title={`Last synced: ${formatRelative(lastSyncedAt)}`}
      disabled={isFetching}
      onClick={(e) => {
        e.preventDefault()
        refetch()
      }}
      className="p-1 rounded text-gray-400 hover:text-blue-500 hover:bg-blue-50 dark:hover:bg-blue-900/30 transition-colors disabled:opacity-50"
    >
      <svg
        className={`w-4 h-4 ${isFetching ? 'animate-spin' : ''}`}
        fill="none"
        viewBox="0 0 24 24"
        stroke="currentColor"
        strokeWidth={2}
      >
        <path
          strokeLinecap="round"
          strokeLinejoin="round"
          d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15"
        />
      </svg>
    </button>
  )
}

export function RepoCoverageCard({ coverage: cov }: RepoCoverageCardProps) {
  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4 space-y-3">
      {/* Header */}
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <p className="text-sm font-semibold text-gray-900 dark:text-white truncate">
            {cov.repositoryName}
          </p>
          {cov.currentTag && (
            <p className="text-xs text-gray-400 font-mono mt-0.5">
              {cov.currentTag} → {cov.nextVersion ?? '…'}
            </p>
          )}
          {cov.jiraFixVersionName && (
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
              Fix version: <span className="font-mono">{cov.jiraFixVersionName}</span>
            </p>
          )}
        </div>
        <div className="flex items-center gap-1 shrink-0">
          <HealthPill matchRate={cov.matchRate} health={cov.health} />
          <ResyncButton repoId={cov.repositoryId} lastSyncedAt={cov.lastSyncedAt} />
        </div>
      </div>

      {/* Unsupported state */}
      {!cov.supported && (
        <p className="text-xs text-amber-600 dark:text-amber-400">
          {cov.unsupportedReason}
        </p>
      )}

      {/* Counters */}
      {cov.supported && (
        <div className="grid grid-cols-3 gap-2 pt-1">
          <CounterBadge label="commits" value={cov.counts.commitCount} />
          <CounterBadge label="git tickets" value={cov.counts.gitTicketCount} />
          <CounterBadge label="jira tickets" value={cov.counts.jiraTicketCount} />
        </div>
      )}

      {/* Bucket counts strip */}
      {cov.supported && (
        <div className="flex items-center gap-3 text-xs">
          <span className="flex items-center gap-1">
            <span className="inline-block w-2 h-2 rounded-full bg-green-500" />
            <span className="text-gray-600 dark:text-gray-400">
              In both: <strong>{cov.counts.inBothCount}</strong>
            </span>
          </span>
          <span className="flex items-center gap-1">
            <span className="inline-block w-2 h-2 rounded-full bg-amber-500" />
            <span className="text-gray-600 dark:text-gray-400">
              Jira only: <strong>{cov.counts.jiraOnlyCount}</strong>
            </span>
          </span>
          <span className="flex items-center gap-1">
            <span className="inline-block w-2 h-2 rounded-full bg-red-500" />
            <span className="text-gray-600 dark:text-gray-400">
              Git only: <strong>{cov.counts.gitOnlyCount}</strong>
            </span>
          </span>
        </div>
      )}

      {/* Footer */}
      <div className="flex items-center justify-between pt-1 border-t border-gray-100 dark:border-gray-800">
        <span className="text-xs text-gray-400">
          Synced {formatRelative(cov.lastSyncedAt)}
        </span>
        <Link
          to={`/repositories/${cov.repositoryId}`}
          className="text-xs text-blue-600 dark:text-blue-400 hover:underline"
        >
          View details →
        </Link>
      </div>
    </div>
  )
}

export function RepoCoverageCardSkeleton() {
  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4 space-y-3 animate-pulse">
      <div className="flex items-start justify-between gap-2">
        <div className="space-y-1.5">
          <div className="h-4 w-32 rounded bg-gray-200 dark:bg-gray-700" />
          <div className="h-3 w-20 rounded bg-gray-100 dark:bg-gray-800" />
        </div>
        <div className="h-5 w-20 rounded-full bg-gray-200 dark:bg-gray-700" />
      </div>
      <div className="grid grid-cols-3 gap-2">
        {[0, 1, 2].map((i) => (
          <div key={i} className="space-y-1 text-center">
            <div className="h-6 w-8 mx-auto rounded bg-gray-200 dark:bg-gray-700" />
            <div className="h-3 w-12 mx-auto rounded bg-gray-100 dark:bg-gray-800" />
          </div>
        ))}
      </div>
    </div>
  )
}
