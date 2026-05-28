import { useState } from 'react'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'
import { useWizardStore } from './store/useWizardStore'

type PreparedReleaseDto = components['schemas']['PreparedReleaseDto']

interface ReconciliationRefreshBarProps {
  releaseId: string
}

export function ReconciliationRefreshBar({ releaseId }: ReconciliationRefreshBarProps) {
  const { reconciliation, reRenderPages } = useWizardStore()
  const [isRefreshing, setIsRefreshing] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const { ran, stale, data } = reconciliation
  const isDisabled = !ran || stale || isRefreshing

  const handleRefresh = async () => {
    if (!data) return
    setIsRefreshing(true)
    setError(null)
    try {
      const res = await apiFetch(`/api/v1/releases/${releaseId}/prepare-pages`, {
        method: 'POST',
        body: JSON.stringify({ reconciliationData: data }),
      })
      if (!res.ok) {
        const body = await res.json().catch(() => ({}))
        throw new Error(body?.title ?? body?.message ?? 'Failed to refresh pages')
      }
      const result: PreparedReleaseDto = await res.json()
      reRenderPages(result.pages)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Refresh failed')
    } finally {
      setIsRefreshing(false)
    }
  }

  return (
    <div className="flex items-center gap-3 rounded-md border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800/50 px-4 py-2.5">
      {/* Status badge */}
      <div className="flex items-center gap-1.5 text-xs font-medium shrink-0">
        <span className="text-gray-500 dark:text-gray-400">Reconciliation:</span>
        {!ran && (
          <span className="inline-flex items-center rounded-full px-2 py-0.5 bg-gray-100 dark:bg-gray-700 text-gray-600 dark:text-gray-400">
            not run
          </span>
        )}
        {ran && !stale && (
          <span className="inline-flex items-center rounded-full px-2 py-0.5 bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400">
            ran
          </span>
        )}
        {ran && stale && (
          <span className="inline-flex items-center rounded-full px-2 py-0.5 bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400">
            stale — re-run reconciliation to refresh
          </span>
        )}
      </div>

      <div className="flex-1" />

      {error && (
        <span className="text-xs text-red-600 dark:text-red-400 shrink-0">{error}</span>
      )}

      <button
        onClick={handleRefresh}
        disabled={isDisabled}
        className="shrink-0 rounded-md bg-blue-600 px-3 py-1.5 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
      >
        {isRefreshing ? 'Refreshing…' : 'Refresh pages with reconciliation data'}
      </button>
    </div>
  )
}
