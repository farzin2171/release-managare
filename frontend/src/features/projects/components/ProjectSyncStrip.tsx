import { useEffect, useState } from 'react'
import { useProjectSync } from '../hooks/useProjectSync'
import { ProjectSyncRunDrawer } from './ProjectSyncRunDrawer'
import type { ProjectSyncDto } from '../../../lib/api/syncApi'

interface Props {
  projectId: string
}

function RelativeTime({ isoString }: { isoString: string }) {
  const diff = Math.floor((Date.now() - new Date(isoString).getTime()) / 1000)
  if (diff < 60) return <>just now</>
  if (diff < 3600) return <>{Math.floor(diff / 60)}m ago</>
  if (diff < 86400) return <>{Math.floor(diff / 3600)}h ago</>
  return <>{Math.floor(diff / 86400)}d ago</>
}

function SyncSummary({ sync }: { sync: ProjectSyncDto }) {
  return (
    <span className="text-xs text-gray-500 dark:text-gray-400">
      {sync.succeededCount} succeeded · {sync.failedCount} failed · {sync.skippedCount} skipped
    </span>
  )
}

export function ProjectSyncStrip({ projectId }: Props) {
  const { activeSsync, latestSync, isRunning, start, cancel, isStarting, isCancelling } =
    useProjectSync(projectId)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [justCompleted, setJustCompleted] = useState<ProjectSyncDto | null>(null)

  // Auto-dismiss "just completed" state after 30s
  useEffect(() => {
    if (!justCompleted) return
    const t = setTimeout(() => setJustCompleted(null), 30_000)
    return () => clearTimeout(t)
  }, [justCompleted])

  // Detect transition from running to done
  const prevRunningRef = { current: false }
  useEffect(() => {
    if (prevRunningRef.current && !isRunning && latestSync) {
      setJustCompleted(latestSync)
    }
    prevRunningRef.current = isRunning
  })

  if (isRunning && activeSsync) {
    const done = activeSsync.succeededCount + activeSsync.failedCount + activeSsync.skippedCount
    return (
      <div
        className="flex items-center justify-between px-4 py-2.5 rounded-lg text-sm"
        style={{
          backgroundColor: 'var(--color-background-info, rgba(59,130,246,0.08))',
          border: '1px solid var(--color-border-info, rgba(59,130,246,0.3))',
        }}
      >
        <div className="flex items-center gap-2">
          <svg className="animate-spin h-4 w-4 text-blue-500 shrink-0" viewBox="0 0 24 24" fill="none">
            <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
            <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
          </svg>
          <span className="text-blue-700 dark:text-blue-300 font-medium">
            Syncing… {done} of {activeSsync.totalRepos} complete
          </span>
        </div>
        <button
          onClick={() => cancel()}
          disabled={isCancelling}
          className="text-xs px-3 py-1 rounded-md bg-white dark:bg-gray-900 border border-blue-300 dark:border-blue-600 text-blue-600 dark:text-blue-400 hover:bg-blue-50 dark:hover:bg-blue-950 transition-colors disabled:opacity-50"
        >
          {isCancelling ? 'Cancelling…' : 'Cancel'}
        </button>
      </div>
    )
  }

  if (justCompleted) {
    return (
      <div className="flex items-center justify-between px-4 py-2.5 rounded-lg bg-green-50 dark:bg-green-950 border border-green-200 dark:border-green-800 text-sm">
        <div className="flex items-center gap-3">
          <span className="text-green-700 dark:text-green-300 font-medium">Sync complete</span>
          <SyncSummary sync={justCompleted} />
        </div>
        <button
          onClick={() => setDrawerOpen(true)}
          className="text-xs text-green-700 dark:text-green-400 underline underline-offset-2"
        >
          View run
        </button>
      </div>
    )
  }

  return (
    <>
      <div className="flex items-center justify-between px-4 py-2.5 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 text-sm">
        <div className="flex items-center gap-3">
          {latestSync?.completedAt ? (
            <>
              <span className="text-gray-500 dark:text-gray-400">
                Project last synced <RelativeTime isoString={latestSync.completedAt} />
              </span>
              <SyncSummary sync={latestSync} />
            </>
          ) : (
            <span className="text-gray-400 dark:text-gray-500">Never synced</span>
          )}
        </div>
        <div className="flex items-center gap-2">
          {latestSync && (
            <button
              onClick={() => setDrawerOpen(true)}
              className="text-xs text-blue-600 dark:text-blue-400 underline underline-offset-2"
            >
              View run
            </button>
          )}
          <button
            onClick={() => start()}
            disabled={isStarting}
            className="text-xs font-medium px-3 py-1 rounded-md bg-blue-600 text-white hover:bg-blue-700 transition-colors disabled:opacity-50"
          >
            {isStarting ? 'Starting…' : 'Sync project'}
          </button>
        </div>
      </div>
      <ProjectSyncRunDrawer
        projectId={projectId}
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
      />
    </>
  )
}
