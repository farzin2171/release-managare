import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { getRepositoryTags, setLatestTag } from '../api/repositoriesApi'
import type { components } from '../../../lib/api'

type RepositoryTagDto = components['schemas']['RepositoryTagDto']

interface TagPickerDialogProps {
  repositoryId: string
  projectId?: string
  onClose: () => void
  onSuccess: () => void
}

function formatDate(iso: string | null): string {
  if (!iso) return '—'
  return new Date(iso).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

export function TagPickerDialog({ repositoryId, projectId, onClose, onSuccess }: TagPickerDialogProps) {
  const qc = useQueryClient()
  const [search, setSearch] = useState('')
  const [selected, setSelected] = useState<RepositoryTagDto | null>(null)

  const { data: tags = [], isLoading, isError, refetch } = useQuery<RepositoryTagDto[]>({
    queryKey: ['repository', repositoryId, 'tags'],
    queryFn: () => getRepositoryTags(repositoryId),
    staleTime: 0,
  })

  const { mutate: pinTag, isPending } = useMutation({
    mutationFn: () => setLatestTag(repositoryId, selected!.name),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['repository', repositoryId] })
      if (projectId) qc.invalidateQueries({ queryKey: ['project', projectId] })
      onSuccess()
      onClose()
    },
  })

  const filtered = tags
    .filter((t) => !search || t.name.toLowerCase().includes(search.toLowerCase()))
    .sort((a, b) => {
      if (a.commitDate && b.commitDate) return b.commitDate.localeCompare(a.commitDate)
      return 0
    })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      <div
        className="absolute inset-0 bg-black/40"
        onClick={onClose}
      />
      <div className="relative z-10 w-full max-w-2xl mx-4 bg-white dark:bg-gray-900 rounded-lg shadow-xl border border-gray-200 dark:border-gray-700 flex flex-col max-h-[80vh]">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-700 shrink-0">
          <h2 className="text-base font-semibold text-gray-900 dark:text-white">Select a tag</h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
          >
            ✕
          </button>
        </div>

        {/* Search */}
        <div className="px-6 py-3 border-b border-gray-200 dark:border-gray-700 shrink-0">
          <input
            type="search"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Filter tags…"
            disabled={isLoading}
            className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-800 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
          />
        </div>

        {/* Tag list */}
        <div className="overflow-y-auto flex-1">
          {isLoading ? (
            <div className="px-6 py-8 space-y-3">
              {[1, 2, 3, 4].map((i) => (
                <div key={i} className="h-10 bg-gray-100 dark:bg-gray-800 rounded animate-pulse" />
              ))}
            </div>
          ) : isError ? (
            <div className="px-6 py-8 text-center">
              <p className="text-sm text-red-600 dark:text-red-400 mb-3">Failed to load tags from provider.</p>
              <button
                onClick={() => refetch()}
                className="text-sm text-blue-600 dark:text-blue-400 hover:underline"
              >
                Retry
              </button>
            </div>
          ) : filtered.length === 0 ? (
            <p className="px-6 py-8 text-sm text-gray-500 dark:text-gray-400 text-center">
              {search ? 'No tags match your filter.' : 'No tags found in this repository.'}
            </p>
          ) : (
            <table className="w-full text-sm">
              <thead className="bg-gray-50 dark:bg-gray-800 sticky top-0">
                <tr>
                  <th className="px-4 py-2 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 w-8" />
                  <th className="px-4 py-2 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Tag</th>
                  <th className="px-4 py-2 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Commit</th>
                  <th className="px-4 py-2 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Date</th>
                  <th className="px-4 py-2 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">Author</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100 dark:divide-gray-800">
                {filtered.map((tag) => {
                  const isSelected = selected?.name === tag.name
                  return (
                    <tr
                      key={tag.name}
                      onClick={() => setSelected(tag)}
                      className={`cursor-pointer transition-colors ${
                        isSelected
                          ? 'bg-blue-50 dark:bg-blue-900/30'
                          : 'hover:bg-gray-50 dark:hover:bg-gray-800/50'
                      }`}
                    >
                      <td className="px-4 py-2.5">
                        <span
                          className={`inline-block h-4 w-4 rounded-full border-2 ${
                            isSelected
                              ? 'border-blue-600 bg-blue-600'
                              : 'border-gray-300 dark:border-gray-600'
                          }`}
                        />
                      </td>
                      <td className="px-4 py-2.5 font-mono font-medium text-gray-900 dark:text-white">
                        {tag.name}
                      </td>
                      <td className="px-4 py-2.5 font-mono text-xs text-gray-500 dark:text-gray-400">
                        {tag.commitSha.slice(0, 7)}
                      </td>
                      <td className="px-4 py-2.5 text-gray-500 dark:text-gray-400">
                        {formatDate(tag.commitDate)}
                      </td>
                      <td className="px-4 py-2.5 text-gray-500 dark:text-gray-400 truncate max-w-[160px]">
                        {tag.authorName ?? '—'}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          )}
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-3 px-6 py-4 border-t border-gray-200 dark:border-gray-700 shrink-0">
          <button
            onClick={onClose}
            className="px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-md transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={() => pinTag()}
            disabled={!selected || isPending || isLoading}
            className="px-4 py-2 text-sm font-medium bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            {isPending ? 'Saving…' : 'Confirm'}
          </button>
        </div>
      </div>
    </div>
  )
}
