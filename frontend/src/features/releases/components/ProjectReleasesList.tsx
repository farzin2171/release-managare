import { useState, useEffect, useRef } from 'react'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'

type ReleaseSummaryDto = components['schemas']['ReleaseSummaryDto']

type StatusFilter = 'All' | 'Draft' | 'Published' | 'Archived'

const STATUS_OPTIONS: StatusFilter[] = ['All', 'Draft', 'Published', 'Archived']

const STATUS_BADGE: Record<string, string> = {
  Draft: 'bg-yellow-100 text-yellow-700 dark:bg-yellow-900/30 dark:text-yellow-400',
  Published: 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400',
  Archived: 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300',
}

function useDebounce(value: string, delayMs: number) {
  const [debounced, setDebounced] = useState(value)
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  useEffect(() => {
    if (timerRef.current) clearTimeout(timerRef.current)
    timerRef.current = setTimeout(() => setDebounced(value), delayMs)
    return () => { if (timerRef.current) clearTimeout(timerRef.current) }
  }, [value, delayMs])
  return debounced
}

interface Props {
  projectId: string
}

export function ProjectReleasesList({ projectId }: Props) {
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('All')
  const [searchInput, setSearchInput] = useState('')
  const debouncedSearch = useDebounce(searchInput, 300)

  const queryParams = new URLSearchParams()
  if (statusFilter !== 'All') queryParams.set('status', statusFilter)
  if (debouncedSearch) queryParams.set('search', debouncedSearch)
  queryParams.set('sort', 'createdAt')
  queryParams.set('order', 'desc')

  const { data: releases = [], isLoading, isError } = useQuery<ReleaseSummaryDto[]>({
    queryKey: ['project-releases', projectId, statusFilter, debouncedSearch],
    queryFn: () =>
      apiFetch(`/api/v1/projects/${projectId}/releases?${queryParams.toString()}`).then((r) => r.json()),
    enabled: !!projectId,
  })

  return (
    <div className="space-y-3">
      {/* Controls */}
      <div className="flex items-center gap-3 flex-wrap">
        {/* Status filter */}
        <div className="flex rounded-md border border-gray-200 dark:border-gray-700 overflow-hidden">
          {STATUS_OPTIONS.map((s) => (
            <button
              key={s}
              onClick={() => setStatusFilter(s)}
              className={`px-3 py-1.5 text-xs font-medium transition-colors ${
                statusFilter === s
                  ? 'bg-blue-600 text-white'
                  : 'bg-white dark:bg-gray-900 text-gray-600 dark:text-gray-400 hover:bg-gray-50 dark:hover:bg-gray-800'
              }`}
            >
              {s}
            </button>
          ))}
        </div>

        {/* Search */}
        <input
          type="search"
          value={searchInput}
          onChange={(e) => setSearchInput(e.target.value)}
          placeholder="Search releases…"
          className="flex-1 min-w-[180px] max-w-xs rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-900 text-sm px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      {/* Table */}
      {isLoading ? (
        <p className="text-sm text-gray-500 py-4">Loading releases…</p>
      ) : isError ? (
        <p className="text-sm text-red-500 py-4">Failed to load releases.</p>
      ) : releases.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 dark:border-gray-600 p-8 text-center">
          <p className="text-sm text-gray-500">
            {debouncedSearch || statusFilter !== 'All'
              ? 'No releases match your filters.'
              : 'No releases yet for this project.'}
          </p>
        </div>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
            <thead className="bg-gray-50 dark:bg-gray-800">
              <tr>
                <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Name</th>
                <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Version</th>
                <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Status</th>
                <th className="px-4 py-2.5 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Repos</th>
                <th className="px-4 py-2.5 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Created</th>
              </tr>
            </thead>
            <tbody className="bg-white dark:bg-gray-900 divide-y divide-gray-100 dark:divide-gray-800">
              {releases.map((rel) => (
                <tr
                  key={rel.id}
                  className="hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors cursor-pointer"
                >
                  <td className="px-4 py-3">
                    <Link
                      to={`/projects/${projectId}/releases/${rel.id}`}
                      className="font-medium text-gray-900 dark:text-white hover:text-blue-600 dark:hover:text-blue-400"
                    >
                      {rel.name}
                    </Link>
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-gray-600 dark:text-gray-400">
                    {rel.version}
                  </td>
                  <td className="px-4 py-3">
                    <span
                      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_BADGE[rel.status] ?? STATUS_BADGE['Archived']}`}
                    >
                      {rel.status}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums text-gray-600 dark:text-gray-400">
                    {rel.repoCount}
                  </td>
                  <td className="px-4 py-3 text-right text-xs text-gray-500 dark:text-gray-400 whitespace-nowrap">
                    {new Date(rel.createdAt).toLocaleDateString()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
