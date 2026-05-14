import { useState } from 'react'
import { useMutation } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'

type ReleaseDetailDto = components['schemas']['ReleaseDetailDto']

interface StepPublishProps {
  releaseId: string
  version: string
  onBack: () => void
  onPublished: (release: ReleaseDetailDto) => void
}

export function StepPublish({ releaseId, version, onBack, onPublished }: StepPublishProps) {
  const [confirmed, setConfirmed] = useState(false)
  const [published, setPublished] = useState<ReleaseDetailDto | null>(null)

  const publishMutation = useMutation({
    mutationFn: () =>
      apiFetch(`/api/v1/releases/${releaseId}/publish`, { method: 'POST' }).then((r) => {
        if (!r.ok) throw new Error('Publish failed')
        return r.json() as Promise<ReleaseDetailDto>
      }),
    onSuccess: (release) => {
      setPublished(release)
      onPublished(release)
    },
  })

  if (published) {
    return (
      <div className="space-y-6">
        <div className="flex items-start gap-3 p-4 rounded-lg bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800">
          <span className="text-green-600 dark:text-green-400 text-xl leading-none">✓</span>
          <div>
            <p className="font-semibold text-green-800 dark:text-green-300">
              Release {published.version} published successfully
            </p>
            <p className="mt-1 text-sm text-green-700 dark:text-green-400">
              The release is now locked and read-only.
            </p>
          </div>
        </div>

        {published.confluencePageUrl && (
          <div className="space-y-1">
            <p className="text-sm font-medium text-gray-700 dark:text-gray-300">Confluence page</p>
            <a
              href={published.confluencePageUrl}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1.5 text-sm text-blue-600 hover:underline dark:text-blue-400 break-all"
            >
              {published.confluencePageUrl}
              <span className="text-xs">↗</span>
            </a>
          </div>
        )}

        <div className="flex justify-end">
          <a
            href={`/releases/${releaseId}`}
            className="px-5 py-2 rounded-md bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 transition-colors"
          >
            View release
          </a>
        </div>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-base font-semibold text-gray-900 dark:text-white">Step 5 — Publish to Confluence</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Publishing will create or update a Confluence page and lock this release as read-only.
        </p>
      </div>

      <div className="rounded-lg border border-yellow-200 dark:border-yellow-800 bg-yellow-50 dark:bg-yellow-900/20 p-4">
        <p className="text-sm font-medium text-yellow-800 dark:text-yellow-300 mb-1">Before publishing</p>
        <ul className="text-sm text-yellow-700 dark:text-yellow-400 list-disc list-inside space-y-0.5">
          <li>The release notes cannot be edited after publishing.</li>
          <li>A Confluence page will be created at the configured space and parent page.</li>
          <li>This action cannot be undone from this interface.</li>
        </ul>
      </div>

      <label className="flex items-center gap-3 cursor-pointer">
        <input
          type="checkbox"
          checked={confirmed}
          onChange={(e) => setConfirmed(e.target.checked)}
          className="accent-blue-600"
        />
        <span className="text-sm text-gray-700 dark:text-gray-300">
          I confirm that the release notes for <strong>{version}</strong> are ready to publish.
        </span>
      </label>

      {publishMutation.isError && (
        <p className="text-sm text-red-500">
          Publish failed. Check your Confluence connection and space configuration.
        </p>
      )}

      <div className="flex justify-between">
        <button
          onClick={onBack}
          disabled={publishMutation.isPending}
          className="px-5 py-2 rounded-md border border-gray-300 dark:border-gray-600 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 disabled:opacity-40 transition-colors"
        >
          Back
        </button>
        <button
          onClick={() => publishMutation.mutate()}
          disabled={!confirmed || publishMutation.isPending}
          className="px-5 py-2 rounded-md bg-green-600 text-white text-sm font-medium hover:bg-green-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        >
          {publishMutation.isPending ? 'Publishing…' : 'Publish release'}
        </button>
      </div>
    </div>
  )
}
