import { useState } from 'react'
import { useRepoCoverage } from '../hooks/useJiraCoverage'
import { HealthPill } from './HealthPill'
import { BucketList } from './BucketList'
import { useAuthStore } from '../../../lib/authStore'
import type { components } from '../../../lib/api'

type CommitSummaryDto = components['schemas']['CommitSummaryDto']

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

function SummaryCard({ label, value }: { label: string; value: string | number }) {
  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4">
      <p className="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">
        {label}
      </p>
      <p className="mt-1 text-3xl font-bold tabular-nums text-gray-900 dark:text-white">
        {value}
      </p>
    </div>
  )
}

function UnmatchedCommitsPanel({ commits }: { commits: CommitSummaryDto[] }) {
  const [open, setOpen] = useState(false)

  if (commits.length === 0) return null

  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900">
      <button
        type="button"
        className="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors rounded-lg"
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
      >
        <div className="flex items-center gap-2">
          <span className="text-sm font-medium text-gray-900 dark:text-white">
            Unmatched commits
          </span>
          <span className="text-xs text-gray-400 font-tabular-nums">({commits.length})</span>
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
          {commits.map((c) => (
            <div
              key={c.sha}
              className="flex items-start gap-3 py-2 text-sm border-b border-gray-100 dark:border-gray-800 last:border-0"
            >
              <span className="font-mono text-xs text-gray-400 shrink-0 mt-0.5">
                {c.sha.slice(0, 7)}
              </span>
              <div className="flex-1 min-w-0">
                <p className="text-gray-700 dark:text-gray-300 truncate">{c.message}</p>
                <p className="text-xs text-gray-400">{c.authorName}</p>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}

interface RepoCoverageTabProps {
  repositoryId: string
}

export function RepoCoverageTab({ repositoryId }: RepoCoverageTabProps) {
  const [refresh, setRefresh] = useState(false)
  const isAdmin = useAuthStore((s) => s.role === 'Admin')
  const { data: cov, isLoading, isError, refetch, isFetching } = useRepoCoverage(repositoryId, refresh)

  if (isLoading) {
    return (
      <div className="py-8 text-center">
        <p className="text-sm text-gray-500">Loading Jira coverage…</p>
      </div>
    )
  }

  if (isError || !cov) {
    return (
      <div className="py-8 text-center">
        <p className="text-sm text-red-500">Failed to load Jira coverage.</p>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      {/* Header strip */}
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div className="min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <h2 className="text-base font-semibold text-gray-900 dark:text-white">
              {cov.repositoryName}
            </h2>
            {cov.currentTag && (
              <span className="text-xs font-mono text-gray-400">
                {cov.currentTag} → {cov.nextVersion ?? '…'}
              </span>
            )}
            <HealthPill matchRate={cov.matchRate} health={cov.health} />
          </div>
          {cov.jiraFixVersionName && (
            <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
              Fix version:{' '}
              {cov.jiraFixVersionExists ? (
                <span className="font-mono">{cov.jiraFixVersionName}</span>
              ) : (
                <span className="font-mono text-amber-600 dark:text-amber-400">
                  {cov.jiraFixVersionName}{' '}
                  <span className="text-gray-400">(not yet created)</span>
                </span>
              )}
            </p>
          )}
          <p className="mt-0.5 text-xs text-gray-400">Synced {formatRelative(cov.lastSyncedAt)}</p>
        </div>
        {isAdmin && (
          <button
            type="button"
            disabled={isFetching}
            title="Force re-sync"
            onClick={() => {
              setRefresh(true)
              refetch()
            }}
            className="flex items-center gap-2 px-3 py-2 rounded-md bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors disabled:opacity-50"
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
            Re-sync
          </button>
        )}
      </div>

      {/* Unsupported warning */}
      {!cov.supported && (
        <div className="rounded-lg border border-amber-200 dark:border-amber-800 bg-amber-50 dark:bg-amber-900/20 p-4">
          <p className="text-sm text-amber-700 dark:text-amber-300">{cov.unsupportedReason}</p>
        </div>
      )}

      {/* Summary cards */}
      {cov.supported && (
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <SummaryCard label="Commits" value={cov.counts.commitCount} />
          <SummaryCard label="Git tickets" value={cov.counts.gitTicketCount} />
          <SummaryCard label="Jira tickets" value={cov.counts.jiraTicketCount} />
          <SummaryCard label="Match rate" value={`${Math.round(cov.matchRate * 100)}%`} />
        </div>
      )}

      {/* Three-bucket list */}
      {cov.supported && (
        <BucketList inBoth={cov.inBoth} jiraOnly={cov.jiraOnly} gitOnly={cov.gitOnly} />
      )}

      {/* Unmatched commits */}
      {cov.supported && <UnmatchedCommitsPanel commits={cov.unmatchedCommits} />}
    </div>
  )
}
