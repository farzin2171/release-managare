import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'
import { clearLatestTag } from '../api/repositoriesApi'
import { useAuthStore } from '../../../lib/authStore'
import { TagPickerDialog } from './TagPickerDialog'
import type { components } from '../../../lib/api'

type RepositoryDto = components['schemas']['RepositoryDto']
type RepositoryTagDto = components['schemas']['RepositoryTagDto']

interface RepositoryDetailSheetProps {
  repository: RepositoryDto
  projectId?: string
  onClose: () => void
}

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime()
  const minutes = Math.floor(diff / 60_000)
  if (minutes < 1) return 'just now'
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return `${Math.floor(hours / 24)}d ago`
}

export function RepositoryDetailSheet({ repository: initialRepo, projectId, onClose }: RepositoryDetailSheetProps) {
  const qc = useQueryClient()
  const isAdmin = useAuthStore((s) => s.role === 'Admin')

  const [showTagPicker, setShowTagPicker] = useState(false)
  const [showClearConfirm, setShowClearConfirm] = useState(false)
  const [successMsg, setSuccessMsg] = useState<string | null>(null)

  // Keep a fresh copy of the repo by watching the repositories list cache
  const { data: repo = initialRepo } = useQuery<RepositoryDto>({
    queryKey: ['repository', initialRepo.id],
    queryFn: async () => {
      const resp = await apiFetch(`/api/v1/repositories`)
      const list = await resp.json() as RepositoryDto[]
      return list.find((r) => r.id === initialRepo.id) ?? initialRepo
    },
    initialData: initialRepo,
    staleTime: 30_000,
  })

  const cachedTags = qc.getQueryData<RepositoryTagDto[]>(['repository', repo.id, 'tags'])
  const pinnedTagStale =
    !!repo.latestTag &&
    Array.isArray(cachedTags) &&
    !cachedTags.some((t) => t.name === repo.latestTag)

  const { mutate: clearTag, isPending: isClearing } = useMutation({
    mutationFn: () => clearLatestTag(repo.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['repository', repo.id] })
      qc.invalidateQueries({ queryKey: ['repositories'] })
      if (projectId) qc.invalidateQueries({ queryKey: ['project', projectId] })
      setShowClearConfirm(false)
      flash('Latest tag cleared.')
    },
  })

  function flash(msg: string) {
    setSuccessMsg(msg)
    setTimeout(() => setSuccessMsg(null), 3000)
  }

  function handleTagSet() {
    qc.invalidateQueries({ queryKey: ['repository', repo.id] })
    qc.invalidateQueries({ queryKey: ['repositories'] })
    flash('Latest tag updated.')
  }

  return (
    <>
      {/* Backdrop */}
      <div className="fixed inset-0 z-40 bg-black/20" onClick={onClose} />

      {/* Sheet panel */}
      <div className="fixed right-0 top-0 z-50 h-full w-full max-w-md bg-white dark:bg-gray-900 shadow-xl flex flex-col border-l border-gray-200 dark:border-gray-700">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-700 shrink-0">
          <h2 className="text-base font-semibold text-gray-900 dark:text-white truncate">
            {repo.name}
          </h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
          >
            ✕
          </button>
        </div>

        {/* Body */}
        <div className="flex-1 overflow-y-auto px-6 py-5 space-y-6">
          {/* Success toast */}
          {successMsg && (
            <div className="rounded-md bg-green-50 dark:bg-green-900/30 border border-green-200 dark:border-green-800 px-4 py-2 text-sm text-green-800 dark:text-green-300">
              {successMsg}
            </div>
          )}

          {/* Latest tag section */}
          <div>
            <p className="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 mb-3">
              Latest tag
            </p>

            {repo.latestTag ? (
              <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 p-4 space-y-1.5">
                <span className="inline-block font-mono font-semibold text-gray-900 dark:text-white text-sm bg-gray-100 dark:bg-gray-700 px-2 py-0.5 rounded">
                  {repo.latestTag}
                </span>
                {repo.latestTagCommitSha && (
                  <p className="font-mono text-xs text-gray-500 dark:text-gray-400">
                    {repo.latestTagCommitSha.slice(0, 7)}
                  </p>
                )}
                {repo.latestTagSetAt && (
                  <p className="text-xs text-gray-500 dark:text-gray-400">
                    Last set {timeAgo(repo.latestTagSetAt)}
                    {repo.latestTagSetBy
                      ? ` by ${repo.latestTagSetBy.email}`
                      : repo.latestTagSetAt
                        ? ' by Unknown user'
                        : ''}
                  </p>
                )}
              </div>
            ) : (
              <p className="text-sm text-gray-500 dark:text-gray-400 italic">Not set</p>
            )}

            {pinnedTagStale && (
              <div className="mt-3 rounded-md border border-amber-300 dark:border-amber-700 bg-amber-50 dark:bg-amber-900/30 px-4 py-3 text-sm text-amber-800 dark:text-amber-300">
                The pinned tag is no longer present in the remote repository. Please select a new one.
              </div>
            )}

            {isAdmin && (
              <div className="mt-3 flex gap-2">
                <button
                  onClick={() => setShowTagPicker(true)}
                  className="px-3 py-1.5 text-sm font-medium bg-blue-600 text-white rounded-md hover:bg-blue-700 transition-colors"
                >
                  {repo.latestTag ? 'Change tag' : 'Set latest tag'}
                </button>
                {repo.latestTag && (
                  <button
                    onClick={() => setShowClearConfirm(true)}
                    className="px-3 py-1.5 text-sm font-medium text-red-600 dark:text-red-400 border border-red-200 dark:border-red-800 rounded-md hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors"
                  >
                    Clear
                  </button>
                )}
              </div>
            )}
          </div>

          {/* Repository details */}
          <div>
            <p className="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 mb-3">
              Details
            </p>
            <dl className="space-y-2 text-sm">
              <div className="flex justify-between gap-2">
                <dt className="text-gray-500 dark:text-gray-400">Azure project</dt>
                <dd className="text-gray-900 dark:text-white font-medium truncate">{repo.azureProjectName}</dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt className="text-gray-500 dark:text-gray-400">Default branch</dt>
                <dd className="font-mono text-xs text-gray-900 dark:text-white">{repo.defaultBranch}</dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt className="text-gray-500 dark:text-gray-400">Tracked</dt>
                <dd className={repo.isTracked ? 'text-green-600 dark:text-green-400' : 'text-gray-400'}>
                  {repo.isTracked ? 'Yes' : 'No'}
                </dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt className="text-gray-500 dark:text-gray-400 shrink-0">Web URL</dt>
                <dd className="truncate">
                  <a
                    href={repo.webUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="text-blue-600 hover:underline dark:text-blue-400 text-xs"
                  >
                    {repo.webUrl}
                  </a>
                </dd>
              </div>
            </dl>
          </div>
        </div>
      </div>

      {/* Tag picker dialog */}
      {showTagPicker && (
        <TagPickerDialog
          repositoryId={repo.id}
          projectId={projectId}
          onClose={() => setShowTagPicker(false)}
          onSuccess={handleTagSet}
        />
      )}

      {/* Clear confirmation */}
      {showClearConfirm && (
        <div className="fixed inset-0 z-60 flex items-center justify-center">
          <div className="absolute inset-0 bg-black/40" onClick={() => setShowClearConfirm(false)} />
          <div className="relative z-10 w-full max-w-sm mx-4 bg-white dark:bg-gray-900 rounded-lg shadow-xl border border-gray-200 dark:border-gray-700 p-6">
            <h3 className="text-sm font-semibold text-gray-900 dark:text-white mb-2">Clear latest tag?</h3>
            <p className="text-sm text-gray-500 dark:text-gray-400 mb-5">
              This will remove the pinned tag from this repository. You can always set a new one later.
            </p>
            <div className="flex justify-end gap-3">
              <button
                onClick={() => setShowClearConfirm(false)}
                className="px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-md transition-colors"
              >
                Cancel
              </button>
              <button
                onClick={() => clearTag()}
                disabled={isClearing}
                className="px-4 py-2 text-sm font-medium bg-red-600 text-white rounded-md hover:bg-red-700 disabled:opacity-50 transition-colors"
              >
                {isClearing ? 'Clearing…' : 'Clear tag'}
              </button>
            </div>
          </div>
        </div>
      )}
    </>
  )
}
