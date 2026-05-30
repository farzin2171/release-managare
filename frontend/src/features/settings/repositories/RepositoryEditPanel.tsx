import { useState } from 'react'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'
import { useAuthStore } from '../../../lib/authStore'
import type { components } from '../../../lib/api'

type RepositoryDto = components['schemas']['RepositoryDto']

interface RepositoryEditPanelProps {
  repository: RepositoryDto
  onClose: () => void
}

export function RepositoryEditPanel({ repository, onClose }: RepositoryEditPanelProps) {
  const qc = useQueryClient()
  const isAdmin = useAuthStore((s) => s.role === 'Admin')

  const [serviceOwner, setServiceOwner] = useState(repository.serviceOwner ?? '')
  const [successMsg, setSuccessMsg] = useState<string | null>(null)
  const [errorMsg, setErrorMsg] = useState<string | null>(null)

  const { mutate: save, isPending } = useMutation({
    mutationFn: () =>
      apiFetch(`/api/v1/repositories/${repository.id}`, {
        method: 'PUT',
        body: JSON.stringify({ serviceOwner: serviceOwner.trim() === '' ? null : serviceOwner.trim() }),
      }).then((r) => {
        if (!r.ok) throw new Error('Failed to save')
        return r.json()
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['repositories'] })
      setErrorMsg(null)
      flash('Service owner saved.')
    },
    onError: () => {
      setErrorMsg('Failed to save service owner. Please try again.')
    },
  })

  function flash(msg: string) {
    setSuccessMsg(msg)
    setTimeout(() => setSuccessMsg(null), 3000)
  }

  return (
    <>
      <div className="fixed inset-0 z-40 bg-black/20" onClick={onClose} />

      <div className="fixed right-0 top-0 z-50 h-full w-full max-w-md bg-white dark:bg-gray-900 shadow-xl flex flex-col border-l border-gray-200 dark:border-gray-700">
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-700 shrink-0">
          <h2 className="text-base font-semibold text-gray-900 dark:text-white truncate">
            {repository.name}
          </h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-300 transition-colors"
          >
            ✕
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-6 py-5 space-y-6">
          {successMsg && (
            <div className="rounded-md bg-green-50 dark:bg-green-900/30 border border-green-200 dark:border-green-800 px-4 py-2 text-sm text-green-800 dark:text-green-300">
              {successMsg}
            </div>
          )}
          {errorMsg && (
            <div className="rounded-md bg-red-50 dark:bg-red-900/30 border border-red-200 dark:border-red-800 px-4 py-2 text-sm text-red-800 dark:text-red-300">
              {errorMsg}
            </div>
          )}

          <div>
            <p className="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 mb-3">
              Ownership
            </p>

            <div className="space-y-1">
              <label
                htmlFor="service-owner"
                className="block text-sm font-medium text-gray-700 dark:text-gray-300"
              >
                Service Owner
              </label>

              {isAdmin ? (
                <input
                  id="service-owner"
                  type="text"
                  value={serviceOwner}
                  onChange={(e) => setServiceOwner(e.target.value)}
                  maxLength={120}
                  placeholder="e.g. Platform Team"
                  className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              ) : (
                <p className="text-sm text-gray-900 dark:text-white">
                  {repository.serviceOwner ?? '—'}
                </p>
              )}
            </div>
          </div>

          <div>
            <p className="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 mb-3">
              Details
            </p>
            <dl className="space-y-2 text-sm">
              <div className="flex justify-between gap-2">
                <dt className="text-gray-500 dark:text-gray-400">Azure project</dt>
                <dd className="text-gray-900 dark:text-white font-medium truncate">{repository.azureProjectName}</dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt className="text-gray-500 dark:text-gray-400">Default branch</dt>
                <dd className="font-mono text-xs text-gray-900 dark:text-white">{repository.defaultBranch}</dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt className="text-gray-500 dark:text-gray-400">Tracked</dt>
                <dd className={repository.isTracked ? 'text-green-600 dark:text-green-400' : 'text-gray-400'}>
                  {repository.isTracked ? 'Yes' : 'No'}
                </dd>
              </div>
              <div className="flex justify-between gap-2">
                <dt className="text-gray-500 dark:text-gray-400 shrink-0">Web URL</dt>
                <dd className="truncate">
                  <a
                    href={repository.webUrl}
                    target="_blank"
                    rel="noreferrer"
                    className="text-blue-600 hover:underline dark:text-blue-400 text-xs"
                  >
                    {repository.webUrl}
                  </a>
                </dd>
              </div>
            </dl>
          </div>
        </div>

        {isAdmin && (
          <div className="px-6 py-4 border-t border-gray-200 dark:border-gray-700 shrink-0 flex justify-end gap-3">
            <button
              onClick={onClose}
              className="px-4 py-2 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-md transition-colors"
            >
              Cancel
            </button>
            <button
              onClick={() => save()}
              disabled={isPending}
              className="px-4 py-2 text-sm font-medium bg-blue-600 text-white rounded-md hover:bg-blue-700 disabled:opacity-50 transition-colors"
            >
              {isPending ? 'Saving…' : 'Save'}
            </button>
          </div>
        )}
      </div>
    </>
  )
}
