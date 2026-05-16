import type { RepositorySyncDto } from '../../../lib/api/syncApi'

interface Props {
  latestSync: RepositorySyncDto | null
  hasTag: boolean
  children: React.ReactNode
}

export function RepoCardSyncOverlay({ latestSync, hasTag, children }: Props) {
  if (!hasTag) {
    return <div className="relative">{children}</div>
  }

  if (latestSync?.status === 'InProgress' || latestSync?.status === 'Pending') {
    return (
      <div className="relative">
        {children}
        <div
          className="absolute inset-0 rounded-lg flex flex-col items-center justify-center gap-1"
          style={{
            backgroundColor: 'var(--color-background-info, rgba(59,130,246,0.08))',
            border: '1px solid var(--color-border-info, rgba(59,130,246,0.3))',
          }}
        >
          <div className="flex items-center gap-2">
            <svg className="animate-spin h-4 w-4 text-blue-500" viewBox="0 0 24 24" fill="none">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
            </svg>
            <p className="text-xs font-medium text-blue-600 dark:text-blue-400">
              {latestSync.currentStep ?? 'Syncing…'}
            </p>
          </div>
        </div>
      </div>
    )
  }

  if (latestSync?.status === 'Failed') {
    return (
      <div className="relative">
        {children}
        <div
          className="absolute inset-0 rounded-lg flex flex-col items-center justify-center gap-1 px-4"
          style={{
            backgroundColor: 'var(--color-background-danger, rgba(239,68,68,0.08))',
            border: '1px solid var(--color-border-danger, rgba(239,68,68,0.3))',
          }}
        >
          <p className="text-xs font-medium text-red-600 dark:text-red-400 text-center line-clamp-2">
            {latestSync.errorMessage ?? 'Sync failed'}
          </p>
        </div>
      </div>
    )
  }

  return <div className="relative">{children}</div>
}
