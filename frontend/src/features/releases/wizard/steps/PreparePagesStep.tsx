import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { apiFetch } from '../../../../lib/apiClient'
import type { components } from '../../../../lib/api'
import { useWizardStore } from '../store/useWizardStore'
import { PreparedPageTab } from '../PreparedPageTab'
import { ReconciliationRefreshBar } from '../ReconciliationRefreshBar'

type PreparedReleaseDto = components['schemas']['PreparedReleaseDto']

interface PreparePagesStepProps {
  releaseId: string
  projectId: string
  onBack: () => void
  onNext: () => void
}

export function PreparePagesStep({ releaseId, projectId, onBack, onNext }: PreparePagesStepProps) {
  const navigate = useNavigate()
  const { pages, initPages } = useWizardStore()

  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [activeTab, setActiveTab] = useState(0)
  const [needsVersionOverride, setNeedsVersionOverride] = useState(false)
  const [adminOverrideVersion, setAdminOverrideVersion] = useState('')
  const [versionError, setVersionError] = useState<string | null>(null)

  useEffect(() => {
    prepare()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [releaseId])

  const prepare = async (overrideVersion?: string) => {
    setIsLoading(true)
    setError(null)
    setVersionError(null)
    try {
      const body: Record<string, unknown> = {}
      if (overrideVersion) body.adminOverrideVersion = overrideVersion

      const res = await apiFetch(`/api/v1/releases/${releaseId}/prepare-pages`, {
        method: 'POST',
        body: JSON.stringify(body),
      })
      if (!res.ok) {
        const respBody = await res.json().catch(() => ({}))
        const errors = respBody?.errors as Record<string, string[]> | undefined
        const code =
          respBody?.extensions?.code ??
          respBody?.code ??
          (errors ? Object.values(errors).flat().find((m) => m.includes('no_')) : '') ??
          ''

        // Extract conflict_code from FluentValidation ProblemDetails
        const conflictCode = respBody?.extensions?.conflictCode ??
          extractConflictCode(respBody)

        if (conflictCode === 'no_release_notes_binding' || code === 'no_release_notes_binding') {
          navigate(`/settings/projects?tab=pages&projectId=${projectId}`, {
            state: { banner: 'Add at least one Release Notes binding before running the wizard.' },
          })
          return
        }
        if (conflictCode === 'no_semver_tag' || code === 'no_semver_tag') {
          setNeedsVersionOverride(true)
          return
        }
        throw new Error(respBody?.title ?? respBody?.message ?? 'Failed to prepare pages')
      }
      const data: PreparedReleaseDto = await res.json()
      setNeedsVersionOverride(false)
      initPages(releaseId, data.pages)
      setActiveTab(0)
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to prepare pages')
    } finally {
      setIsLoading(false)
    }
  }

  const handleOverrideSubmit = () => {
    const v = adminOverrideVersion.trim()
    if (!v) {
      setVersionError('Please enter a version, e.g. 1.0.0')
      return
    }
    if (!/^\d+\.\d+\.\d+/.test(v)) {
      setVersionError('Version must be in semver format, e.g. 1.0.0')
      return
    }
    prepare(v)
  }

  function extractConflictCode(body: Record<string, unknown>): string {
    if (!body?.errors) return ''
    const allMessages = Object.values(body.errors as Record<string, string[]>).flat()
    for (const msg of allMessages) {
      if (msg === 'no_release_notes_binding') return 'no_release_notes_binding'
      if (msg === 'no_semver_tag') return 'no_semver_tag'
    }
    return ''
  }

  const activePages = pages.length > 0 ? pages : []

  if (isLoading) {
    return (
      <div className="flex items-center gap-3 py-8">
        <div className="h-5 w-5 rounded-full border-2 border-blue-600 border-t-transparent animate-spin" />
        <span className="text-sm text-gray-500">Preparing pages…</span>
      </div>
    )
  }

  if (needsVersionOverride) {
    return (
      <div className="space-y-4 max-w-md">
        <div className="rounded-md bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 px-4 py-3">
          <p className="text-sm font-medium text-amber-800 dark:text-amber-300">Version could not be determined</p>
          <p className="text-sm text-amber-700 dark:text-amber-400 mt-1">
            The primary repository has no semver tag. Enter the target version to continue.
          </p>
        </div>
        <div className="space-y-2">
          <label className="block text-sm font-medium text-gray-700 dark:text-gray-300">
            Override version
          </label>
          <input
            type="text"
            value={adminOverrideVersion}
            onChange={(e) => setAdminOverrideVersion(e.target.value)}
            placeholder="e.g. 1.0.0"
            className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            onKeyDown={(e) => e.key === 'Enter' && handleOverrideSubmit()}
          />
          {versionError && <p className="text-xs text-red-500">{versionError}</p>}
        </div>
        <div className="flex gap-3">
          <button
            onClick={onBack}
            className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700"
          >
            Back
          </button>
          <button
            onClick={handleOverrideSubmit}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            Use this version
          </button>
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="space-y-4">
        <div className="rounded-md bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 px-4 py-3">
          <p className="text-sm text-red-700 dark:text-red-300">{error}</p>
        </div>
        <div className="flex gap-3">
          <button
            onClick={onBack}
            className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700"
          >
            Back
          </button>
          <button
            onClick={() => prepare()}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            Retry
          </button>
        </div>
      </div>
    )
  }

  if (activePages.length === 0) {
    return (
      <div className="space-y-4">
        <p className="text-sm text-gray-500">No pages prepared yet.</p>
        <button
          onClick={onBack}
          className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700"
        >
          Back
        </button>
      </div>
    )
  }

  return (
    <div className="space-y-4">
      {/* Reconciliation refresh bar */}
      <ReconciliationRefreshBar releaseId={releaseId} />

      {/* Tab navigation */}
      <div className="border-b border-gray-200 dark:border-gray-700">
        <nav className="-mb-px flex gap-4 overflow-x-auto">
          {activePages.map((slot, idx) => {
            const hasWarning = slot.unknownTokens.length > 0
            const isDirty = slot.draftState.kind === 'edited'
            return (
              <button
                key={slot.bindingId}
                onClick={() => setActiveTab(idx)}
                className={`pb-3 text-sm font-medium border-b-2 whitespace-nowrap transition-colors flex items-center gap-1.5 ${
                  activeTab === idx
                    ? 'border-blue-600 text-blue-600 dark:border-blue-400 dark:text-blue-400'
                    : 'border-transparent text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200'
                }`}
              >
                {(() => {
                  const t =
                    slot.draftState.kind === 'server'
                      ? slot.serverTitle
                      : slot.draftState.kind === 'edited'
                      ? slot.draftState.title
                      : slot.draftState.draftTitle
                  return t.slice(0, 30) + (t.length > 30 ? '…' : '')
                })()}
                {isDirty && (
                  <span className="inline-block w-1.5 h-1.5 rounded-full bg-blue-500 shrink-0" title="Edited" />
                )}
                {hasWarning && (
                  <span className="inline-flex items-center rounded px-1 py-0 text-xs bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-400">
                    !
                  </span>
                )}
              </button>
            )
          })}
        </nav>
      </div>

      {/* Active tab content */}
      {activePages[activeTab] && (
        <PreparedPageTab slot={activePages[activeTab]} />
      )}

      {/* Navigation */}
      <div className="flex gap-3 pt-4 border-t border-gray-200 dark:border-gray-700">
        <button
          onClick={onBack}
          className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700"
        >
          Back
        </button>
        <button
          onClick={onNext}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          Next: Publish
        </button>
      </div>
    </div>
  )
}
