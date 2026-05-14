import { useState } from 'react'
import { useParams, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '../../lib/apiClient'
import type { components } from '../../lib/api'
import { ChangeFilters } from './ChangeFilters'
import { TicketGroupList } from './TicketGroupList'
import { UnscopedBucket } from './UnscopedBucket'
import { CommitsView } from './CommitsView'
import { ContributorsView } from './ContributorsView'

type RepositoryChangesDto = components['schemas']['RepositoryChangesDto']

type ViewMode = 'tickets' | 'commits' | 'contributors'

function SummaryCard({ label, value, accent }: { label: string; value: number; accent?: boolean }) {
  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4">
      <p className="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">
        {label}
      </p>
      <p className={`mt-1 text-3xl font-bold tabular-nums ${accent ? 'text-red-600 dark:text-red-400' : 'text-gray-900 dark:text-white'}`}>
        {value}
      </p>
    </div>
  )
}

export function RepositoryDetail() {
  const { id } = useParams<{ id: string }>()
  const [viewMode, setViewMode] = useState<ViewMode>('tickets')
  const [typeFilter, setTypeFilter] = useState('')
  const [search, setSearch] = useState('')

  const { data, isLoading, isError } = useQuery<RepositoryChangesDto>({
    queryKey: ['repository-changes', id],
    queryFn: () => apiFetch(`/api/v1/repositories/${id}/changes`).then((r) => r.json()),
    enabled: !!id,
  })

  if (isLoading) {
    return (
      <div className="p-8">
        <p className="text-sm text-gray-500">Loading changes…</p>
      </div>
    )
  }

  if (isError || !data) {
    return (
      <div className="p-8">
        <p className="text-sm text-red-500">Failed to load repository changes.</p>
      </div>
    )
  }

  const { summary, groups, unscoped, repositoryName, fromTag, toTag } = data

  const tabs: { key: ViewMode; label: string }[] = [
    { key: 'tickets', label: 'Tickets' },
    { key: 'commits', label: 'Commits' },
    { key: 'contributors', label: 'Contributors' },
  ]

  return (
    <div className="max-w-6xl space-y-6 p-6">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
        <Link to="/projects" className="hover:text-gray-700 dark:hover:text-gray-200">
          Projects
        </Link>
        <span>/</span>
        <span className="text-gray-900 dark:text-white font-medium">{repositoryName}</span>
      </nav>

      {/* Header */}
      <div>
        <h1 className="text-xl font-semibold text-gray-900 dark:text-white">{repositoryName}</h1>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Changes from{' '}
          <span className="font-mono text-xs">{fromTag ?? 'beginning'}</span>
          {' '}to{' '}
          <span className="font-mono text-xs">{toTag}</span>
        </p>
      </div>

      {/* Summary cards */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
        <SummaryCard label="Commits" value={summary.commitCount} />
        <SummaryCard label="Tickets" value={summary.ticketCount} />
        <SummaryCard label="Breaking" value={summary.breakingCount} accent={summary.breakingCount > 0} />
        <SummaryCard label="Contributors" value={summary.contributorCount} />
      </div>

      {/* Tab bar + filters */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 border-b border-gray-200 dark:border-gray-700 pb-4">
        <div className="flex gap-1">
          {tabs.map((t) => (
            <button
              key={t.key}
              onClick={() => setViewMode(t.key)}
              className={`px-4 py-2 rounded-md text-sm font-medium transition-colors ${
                viewMode === t.key
                  ? 'bg-blue-600 text-white'
                  : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700'
              }`}
            >
              {t.label}
            </button>
          ))}
        </div>
        {(viewMode === 'tickets' || viewMode === 'commits') && (
          <ChangeFilters
            type={typeFilter}
            setType={setTypeFilter}
            search={search}
            setSearch={setSearch}
          />
        )}
      </div>

      {/* Content */}
      {viewMode === 'tickets' && (
        <div className="space-y-6">
          <TicketGroupList groups={groups} search={search} typeFilter={typeFilter} />
          <UnscopedBucket commits={unscoped} />
        </div>
      )}
      {viewMode === 'commits' && (
        <CommitsView groups={groups} unscoped={unscoped} typeFilter={typeFilter} search={search} />
      )}
      {viewMode === 'contributors' && (
        <ContributorsView groups={groups} unscoped={unscoped} />
      )}
    </div>
  )
}
