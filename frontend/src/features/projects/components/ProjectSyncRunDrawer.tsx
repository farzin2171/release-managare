import { useQuery } from '@tanstack/react-query'
import { getLatestProjectSync, type ProjectSyncDto, type RepositorySyncDto } from '../../../lib/api/syncApi'

interface Props {
  projectId: string
  open: boolean
  onClose: () => void
}

function StatusBadge({ status }: { status: RepositorySyncDto['status'] | ProjectSyncDto['status'] }) {
  const colours: Record<string, string> = {
    Succeeded: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
    PartiallyFailed: 'bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200',
    Failed: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200',
    Cancelled: 'bg-gray-100 text-gray-600 dark:bg-gray-800 dark:text-gray-400',
    Skipped: 'bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400',
    InProgress: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200',
    Pending: 'bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400',
  }
  return (
    <span className={`inline-flex px-2 py-0.5 rounded text-xs font-medium ${colours[status] ?? ''}`}>
      {status}
    </span>
  )
}

function ElapsedTime({ startedAt, completedAt }: { startedAt: string; completedAt: string | null }) {
  const ms = completedAt
    ? new Date(completedAt).getTime() - new Date(startedAt).getTime()
    : Date.now() - new Date(startedAt).getTime()
  const s = Math.floor(ms / 1000)
  if (s < 60) return <>{s}s</>
  return <>{Math.floor(s / 60)}m {s % 60}s</>
}

function RepoRow({ sync }: { sync: RepositorySyncDto }) {
  return (
    <div className="flex items-start justify-between gap-4 py-2.5 border-b border-gray-100 dark:border-gray-800 last:border-0">
      <div className="min-w-0">
        <div className="flex items-center gap-2">
          <StatusBadge status={sync.status} />
          <span className="text-sm text-gray-700 dark:text-gray-200 font-mono truncate">{sync.fromTag || '—'}</span>
        </div>
        {sync.errorMessage && (
          <p className="mt-1 text-xs text-red-600 dark:text-red-400 line-clamp-2">{sync.errorMessage}</p>
        )}
      </div>
      <div className="flex items-center gap-4 text-xs text-gray-500 dark:text-gray-400 shrink-0 tabular-nums">
        {sync.status === 'Succeeded' && (
          <>
            <span>{sync.commitCount} commits</span>
            <span>{sync.ticketCount} tickets</span>
          </>
        )}
        <ElapsedTime startedAt={sync.startedAt} completedAt={sync.completedAt} />
      </div>
    </div>
  )
}

export function ProjectSyncRunDrawer({ projectId, open, onClose }: Props) {
  const { data: run, isLoading } = useQuery({
    queryKey: ['project-sync', 'latest', projectId],
    queryFn: () => getLatestProjectSync(projectId),
    enabled: open && !!projectId,
  })

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex">
      <div className="flex-1 bg-black/30" onClick={onClose} />
      <div className="w-full max-w-md bg-white dark:bg-gray-900 shadow-xl flex flex-col">
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-200 dark:border-gray-700">
          <h2 className="text-base font-semibold text-gray-900 dark:text-white">Project sync run</h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 transition-colors"
          >
            ✕
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-5">
          {isLoading && (
            <p className="text-sm text-gray-500">Loading…</p>
          )}
          {!isLoading && !run && (
            <p className="text-sm text-gray-500">No run found.</p>
          )}
          {run && (
            <div className="space-y-4">
              <div className="flex items-center gap-3">
                <StatusBadge status={run.status} />
                <span className="text-sm text-gray-500 dark:text-gray-400">
                  {run.succeededCount} succeeded · {run.failedCount} failed · {run.skippedCount} skipped
                </span>
              </div>
              {run.childSyncs && run.childSyncs.length > 0 ? (
                <div>
                  <p className="text-xs font-medium uppercase tracking-wider text-gray-400 mb-2">Repositories</p>
                  {run.childSyncs.map((s) => (
                    <RepoRow key={s.id} sync={s} />
                  ))}
                </div>
              ) : (
                <p className="text-sm text-gray-400">No repository details available.</p>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
