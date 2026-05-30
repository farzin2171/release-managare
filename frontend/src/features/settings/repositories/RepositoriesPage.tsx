import { useState, useDeferredValue } from 'react'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'
import { RepositoriesTable } from './RepositoriesTable'
import { RepositoryEditPanel } from './RepositoryEditPanel'
import type { components } from '../../../lib/api'

type RepositoryDto = components['schemas']['RepositoryDto']
type GitConnectionDto = components['schemas']['GitConnectionDto']

export function RepositoriesPage() {
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const [connectionId, setConnectionId] = useState('')
  const [togglingId, setTogglingId] = useState<string | null>(null)
  const [selectedRepo, setSelectedRepo] = useState<RepositoryDto | null>(null)
  const deferredSearch = useDeferredValue(search)

  const { data: connections = [] } = useQuery<GitConnectionDto[]>({
    queryKey: ['git-connections'],
    queryFn: () => apiFetch('/api/v1/integrations/git').then((r) => r.json()),
  })

  const queryParams = new URLSearchParams()
  if (deferredSearch) queryParams.set('search', deferredSearch)
  if (connectionId) queryParams.set('connectionId', connectionId)

  const { data: repos = [], isLoading, isFetching } = useQuery<RepositoryDto[]>({
    queryKey: ['repositories', { search: deferredSearch, connectionId }],
    queryFn: () =>
      apiFetch(`/api/v1/repositories?${queryParams.toString()}`).then((r) => r.json()),
  })

  const handleToggleTracked = async (repo: RepositoryDto) => {
    setTogglingId(repo.id)
    try {
      await apiFetch(`/api/v1/repositories/${repo.id}`, {
        method: 'PATCH',
        body: JSON.stringify({ isTracked: !repo.isTracked }),
      })
      qc.invalidateQueries({ queryKey: ['repositories'] })
    } finally {
      setTogglingId(null)
    }
  }

  return (
    <div className="max-w-6xl space-y-6">
      <div>
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Repositories</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Mark repositories as tracked to include them in projects and commit syncs.
        </p>
      </div>

      {/* Filters */}
      <div className="flex gap-3">
        <input
          type="search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Search repositories…"
          className="w-64 rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
        {connections.length > 1 && (
          <select
            value={connectionId}
            onChange={(e) => setConnectionId(e.target.value)}
            className="rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="">All connections</option>
            {connections.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        )}
        {isFetching && !isLoading && (
          <span className="self-center text-xs text-gray-400">Refreshing…</span>
        )}
      </div>

      {isLoading ? (
        <p className="text-sm text-gray-500">Loading…</p>
      ) : repos.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 dark:border-gray-600 p-8 text-center">
          <p className="text-sm text-gray-500 dark:text-gray-400">
            {search || connectionId
              ? 'No repositories match your filters.'
              : 'No repositories found. Sync a Git connection first.'}
          </p>
        </div>
      ) : (
        <RepositoriesTable
          repos={repos}
          togglingId={togglingId}
          onToggleTracked={handleToggleTracked}
          onRowClick={setSelectedRepo}
        />
      )}

      <p className="text-xs text-gray-400 dark:text-gray-500">
        {repos.length} {repos.length === 1 ? 'repository' : 'repositories'} found
        {repos.filter((r) => r.isTracked).length > 0 &&
          ` · ${repos.filter((r) => r.isTracked).length} tracked`}
      </p>

      {selectedRepo && (
        <RepositoryEditPanel
          repository={selectedRepo}
          onClose={() => setSelectedRepo(null)}
        />
      )}
    </div>
  )
}
