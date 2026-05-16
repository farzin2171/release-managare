import { Link } from 'react-router-dom'
import type { components } from '../../../lib/api'
import { useRepositorySync } from '../hooks/useRepositorySync'
import { RepoCardSyncOverlay } from './RepoCardSyncOverlay'
import { RepoCardSyncFooter } from './RepoCardSyncFooter'

type RepositoryChangesDto = components['schemas']['RepositoryChangesDto']

interface Props {
  repo: RepositoryChangesDto
  color: string
  latestTag: string | null
}

function TagChip({ latestTag }: { latestTag: string | null }) {
  if (!latestTag) {
    return (
      <span className="flex items-center gap-1 text-xs font-mono text-amber-600 dark:text-amber-400 shrink-0">
        <span
          className="inline-block w-2 h-2 rounded-full bg-amber-400"
          aria-hidden="true"
        />
        No tag → HEAD
      </span>
    )
  }
  return (
    <span className="text-xs text-gray-400 dark:text-gray-500 font-mono shrink-0">
      {latestTag} → HEAD
    </span>
  )
}

export function RepositoryCard({ repo, color, latestTag }: Props) {
  const { latestSync, trigger, isTriggerPending } = useRepositorySync(repo.repositoryId)
  const s = repo.summary
  const hasTag = !!latestTag

  const displayCommits = latestSync?.status === 'Succeeded' ? latestSync.commitCount : s.commitCount
  const displayTickets = latestSync?.status === 'Succeeded' ? latestSync.ticketCount : s.ticketCount
  const displayBreaking = latestSync?.status === 'Succeeded' ? latestSync.breakingChangeCount : s.breakingCount
  const displayContributors = latestSync?.status === 'Succeeded' ? latestSync.contributorCount : s.contributorCount

  return (
    <Link
      to={`/repositories/${repo.repositoryId}`}
      className="block rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 hover:shadow-md hover:border-blue-300 dark:hover:border-blue-600 transition-all"
    >
      <div className="flex items-start justify-between gap-3 mb-4">
        <div className="flex items-center gap-2 min-w-0">
          <span
            className="inline-block w-2.5 h-2.5 rounded-full shrink-0"
            style={{ backgroundColor: color }}
          />
          <span className="text-sm font-semibold text-gray-900 dark:text-white truncate">
            {repo.repositoryName}
          </span>
        </div>
        <TagChip latestTag={latestTag} />
      </div>

      <RepoCardSyncOverlay latestSync={latestSync} hasTag={hasTag}>
        <div className="grid grid-cols-4 gap-3 text-center">
          <div>
            <p className="text-xl font-bold tabular-nums text-gray-900 dark:text-white">{displayCommits}</p>
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">commits</p>
          </div>
          <div>
            <p className="text-xl font-bold tabular-nums text-gray-900 dark:text-white">{displayTickets}</p>
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">tickets</p>
          </div>
          <div>
            <p className={`text-xl font-bold tabular-nums ${displayBreaking > 0 ? 'text-red-600 dark:text-red-400' : 'text-gray-900 dark:text-white'}`}>
              {displayBreaking}
            </p>
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">breaking</p>
          </div>
          <div>
            <p className="text-xl font-bold tabular-nums text-gray-900 dark:text-white">{displayContributors}</p>
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">contributors</p>
          </div>
        </div>
      </RepoCardSyncOverlay>

      <RepoCardSyncFooter
        latestSync={latestSync}
        hasTag={hasTag}
        onSync={trigger}
        isSyncPending={isTriggerPending}
      />
    </Link>
  )
}
