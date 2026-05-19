import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'

export function MaintenancePage() {
  const [successMsg, setSuccessMsg] = useState<string | null>(null)

  function flash(msg: string) {
    setSuccessMsg(msg)
    setTimeout(() => setSuccessMsg(null), 4000)
  }

  const reset = useMutation({
    mutationFn: async () => {
      const res = await apiFetch('/api/v1/admin/database/reset', { method: 'POST' })
      if (!res.ok) {
        const err = await res.json().catch(() => ({}))
        throw new Error((err as { title?: string }).title ?? 'Failed to reset database')
      }
    },
    onSuccess: () => flash('Database reset. All operational data has been cleared.'),
  })

  const handleReset = () => {
    if (
      !window.confirm(
        'This will permanently delete all repositories, projects, commits, releases, and sync history.\n\nSettings (users, integrations, templates) are preserved.\n\nThis cannot be undone. Continue?'
      )
    )
      return
    reset.mutate()
  }

  return (
    <div className="space-y-6 max-w-2xl">
      <div>
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Maintenance</h2>
        <p className="text-sm text-gray-500 dark:text-gray-400 mt-1">
          Destructive operations for development and testing environments.
        </p>
      </div>

      <div className="rounded-lg border border-red-200 dark:border-red-800 bg-red-50 dark:bg-red-900/10 p-6 space-y-4">
        <h3 className="text-sm font-semibold text-red-800 dark:text-red-300 uppercase tracking-wide">
          Danger Zone
        </h3>

        <div className="flex items-start justify-between gap-6">
          <div className="space-y-1">
            <p className="text-sm font-medium text-gray-900 dark:text-white">Reset database</p>
            <p className="text-sm text-gray-500 dark:text-gray-400">
              Deletes all repositories, projects, commits, releases, Jira mirrors, and sync history.
              Users, integration credentials, and release note templates are preserved.
            </p>
          </div>
          <button
            onClick={handleReset}
            disabled={reset.isPending}
            className="shrink-0 rounded-md bg-red-600 px-4 py-2 text-sm font-medium text-white hover:bg-red-700 disabled:opacity-50"
          >
            {reset.isPending ? 'Resetting…' : 'Reset Database'}
          </button>
        </div>

        {successMsg && (
          <div className="rounded-md bg-green-50 dark:bg-green-900/30 border border-green-200 dark:border-green-800 px-4 py-2 text-sm text-green-800 dark:text-green-300">
            {successMsg}
          </div>
        )}

        {reset.isError && (
          <div className="rounded-md bg-red-100 dark:bg-red-900/40 border border-red-300 dark:border-red-700 px-4 py-2 text-sm text-red-800 dark:text-red-300">
            {(reset.error as Error).message}
          </div>
        )}
      </div>
    </div>
  )
}
