import { useState, useEffect, useRef } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'
import { useAuthStore } from '../../../lib/authStore'
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
  const navigate = useNavigate()
  const qc = useQueryClient()
  const isAdmin = useAuthStore((s) => s.role === 'Admin')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('All')
  const [searchInput, setSearchInput] = useState('')
  const debouncedSearch = useDebounce(searchInput, 300)
  const [openMenuId, setOpenMenuId] = useState<string | null>(null)
  const [confirmRelease, setConfirmRelease] = useState<ReleaseSummaryDto | null>(null)
  const [fadingIds, setFadingIds] = useState<Set<string>>(new Set())
  const [toast, setToast] = useState<{ kind: 'success' | 'error'; message: string } | null>(null)

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

  const showToast = (kind: 'success' | 'error', message: string) => {
    setToast({ kind, message })
    setTimeout(() => setToast(null), 4000)
  }

  const deleteRelease = useMutation({
    mutationFn: async (rel: ReleaseSummaryDto) => {
      const res = await apiFetch(`/api/v1/projects/${projectId}/releases/${rel.id}`, { method: 'DELETE' })
      if (res.status === 409) throw Object.assign(new Error('conflict'), { status: 409 })
      if (!res.ok) throw new Error('delete_failed')
    },
    onSuccess: (_, rel) => {
      showToast('success', `Draft release '${rel.name}' deleted.`)
      setFadingIds((prev) => new Set(prev).add(rel.id))
      setTimeout(() => {
        qc.invalidateQueries({ queryKey: ['project-releases', projectId] })
        setFadingIds((prev) => { const s = new Set(prev); s.delete(rel.id); return s })
      }, 600)
    },
    onError: (err: unknown) => {
      if ((err as { status?: number })?.status === 409) {
        showToast('error', 'This release has been published and can no longer be deleted.')
        qc.invalidateQueries({ queryKey: ['project-releases', projectId] })
      } else {
        showToast('error', 'Failed to delete release. Please try again.')
      }
    },
  })

  const handleConfirmDelete = () => {
    if (!confirmRelease) return
    const rel = confirmRelease
    setConfirmRelease(null)
    deleteRelease.mutate(rel)
  }

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
          className="flex-1 min-w-45 max-w-xs rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-900 text-sm px-3 py-1.5 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      {/* Toast */}
      {toast && (
        <div
          className={`rounded-md px-4 py-2 text-sm font-medium ${
            toast.kind === 'success'
              ? 'bg-green-50 text-green-800 dark:bg-green-900/30 dark:text-green-300'
              : 'bg-red-50 text-red-700 dark:bg-red-900/30 dark:text-red-300'
          }`}
        >
          {toast.message}
        </div>
      )}

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
                {isAdmin && <th className="px-2 py-2.5 w-10" />}
              </tr>
            </thead>
            <tbody className="bg-white dark:bg-gray-900 divide-y divide-gray-100 dark:divide-gray-800">
              {releases.map((rel) => (
                <tr
                  key={rel.id}
                  style={{ opacity: fadingIds.has(rel.id) ? 0 : 1, transition: 'opacity 0.5s' }}
                  className="hover:bg-gray-50 dark:hover:bg-gray-800 cursor-pointer"
                  onClick={() => navigate(`/releases/${rel.id}`)}
                >
                  <td className="px-4 py-3">
                    <Link
                      to={`/releases/${rel.id}`}
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
                  {isAdmin && (
                    <td className="px-2 py-3" onClick={(e) => e.stopPropagation()}>
                      {rel.status === 'Draft' && (
                        <div className="relative inline-block">
                          <button
                            onClick={(e) => {
                              e.stopPropagation()
                              setOpenMenuId(openMenuId === rel.id ? null : rel.id)
                            }}
                            className="p-1 rounded text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-700 leading-none text-base"
                            aria-label="Release actions"
                          >
                            ⋮
                          </button>
                          {openMenuId === rel.id && (
                            <>
                              <div
                                className="fixed inset-0 z-10"
                                onClick={() => setOpenMenuId(null)}
                              />
                              <div className="absolute right-0 top-full mt-1 w-44 rounded-md shadow-lg bg-white dark:bg-gray-800 border border-gray-200 dark:border-gray-700 z-20 py-1">
                                <button
                                  onClick={() => {
                                    setOpenMenuId(null)
                                    setConfirmRelease(rel)
                                  }}
                                  className="w-full text-left px-4 py-2 text-sm text-red-600 dark:text-red-400 hover:bg-red-50 dark:hover:bg-gray-700"
                                >
                                  Delete draft
                                </button>
                              </div>
                            </>
                          )}
                        </div>
                      )}
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {/* Confirmation dialog */}
      {confirmRelease && (
        <div className="fixed inset-0 z-50 flex items-center justify-center">
          <div
            className="absolute inset-0 bg-black/40"
            onClick={() => setConfirmRelease(null)}
          />
          <div className="relative z-10 rounded-lg bg-white dark:bg-gray-800 shadow-xl p-6 max-w-sm w-full mx-4">
            <h3 className="text-base font-semibold text-gray-900 dark:text-white mb-2">
              Delete draft release?
            </h3>
            <p className="text-sm text-gray-500 dark:text-gray-400 mb-6">
              Delete draft release &apos;{confirmRelease.name}&apos;? This cannot be undone.
            </p>
            <div className="flex justify-end gap-3">
              <button
                onClick={() => setConfirmRelease(null)}
                className="rounded-md px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 border border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-700"
              >
                Cancel
              </button>
              <button
                onClick={handleConfirmDelete}
                disabled={deleteRelease.isPending}
                className="rounded-md px-4 py-2 text-sm font-medium text-white bg-red-600 hover:bg-red-700 disabled:opacity-50"
              >
                {deleteRelease.isPending ? 'Deleting…' : 'Delete'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
