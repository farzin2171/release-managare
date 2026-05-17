import type { RepositorySyncDto } from '../../../lib/api/syncApi'

interface Props {
  latestSync: RepositorySyncDto | null
  hasTag: boolean
  onSync: () => void
  isSyncPending: boolean
}

function RelativeTime({ isoString }: { isoString: string }) {
  const diff = Math.floor((Date.now() - new Date(isoString).getTime()) / 1000)
  if (diff < 60) return <>just now</>
  if (diff < 3600) return <>{Math.floor(diff / 60)}m ago</>
  if (diff < 86400) return <>{Math.floor(diff / 3600)}h ago</>
  return <>{Math.floor(diff / 86400)}d ago</>
}

export function RepoCardSyncFooter({ latestSync, hasTag, onSync, isSyncPending }: Props) {
  const isActive = latestSync?.status === 'Pending' || latestSync?.status === 'InProgress'
  const isFailed = latestSync?.status === 'Failed'
  const isSucceeded = latestSync?.status === 'Succeeded'

  const buttonDisabled = !hasTag || isActive || isSyncPending

  return (
    <div className="flex items-center justify-between mt-3 pt-3 border-t border-gray-100 dark:border-gray-800">
      <span className="text-xs text-gray-400 dark:text-gray-500">
        {isSucceeded && latestSync.completedAt ? (
          <>Last synced <RelativeTime isoString={latestSync.completedAt} /></>
        ) : (
          'Not synced yet'
        )}
      </span>

      <div className="relative group/sync">
        <button
          onClick={(e) => { e.preventDefault(); e.stopPropagation(); onSync() }}
          disabled={buttonDisabled}
          className={[
            'text-xs font-medium px-3 py-1 rounded-md transition-colors',
            buttonDisabled
              ? 'bg-gray-100 dark:bg-gray-800 text-gray-400 cursor-not-allowed'
              : isFailed
              ? 'bg-red-50 dark:bg-red-950 text-red-600 dark:text-red-400 hover:bg-red-100 dark:hover:bg-red-900'
              : 'bg-blue-50 dark:bg-blue-950 text-blue-600 dark:text-blue-400 hover:bg-blue-100 dark:hover:bg-blue-900',
          ].join(' ')}
        >
          {isActive ? 'Syncing…' : isFailed ? 'Retry' : 'Sync'}
        </button>
        {!hasTag && (
          <div className="absolute bottom-full right-0 mb-1.5 px-2 py-1 text-xs text-white bg-gray-800 dark:bg-gray-600 rounded whitespace-nowrap opacity-0 group-hover/sync:opacity-100 transition-opacity pointer-events-none z-10">
            No tag pinned — pin a tag to enable sync
          </div>
        )}
      </div>
    </div>
  )
}
