import { useParams, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '../../lib/apiClient'
import type { components } from '../../lib/api'

type ProjectDetailDto = components['schemas']['ProjectDetailDto']
type ProjectChangesDto = components['schemas']['ProjectChangesDto']
type RepositoryChangesDto = components['schemas']['RepositoryChangesDto']

function MetricCard({ label, value, accent }: { label: string; value: number; accent?: boolean }) {
  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4">
      <p className="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400">
        {label}
      </p>
      <p className={`mt-1 text-3xl font-bold tabular-nums ${accent ? 'text-red-600 dark:text-red-400' : 'text-gray-900 dark:text-white'}`}>
        {value}
      </p>
    </div>
  )
}

function RepoCard({
  repo,
  color,
}: {
  repo: RepositoryChangesDto
  color: string
}) {
  const s = repo.summary
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
        <span className="text-xs text-gray-400 dark:text-gray-500 font-mono shrink-0">
          {repo.fromTag ?? 'init'} → {repo.toTag}
        </span>
      </div>

      <div className="grid grid-cols-4 gap-3 text-center">
        <div>
          <p className="text-xl font-bold tabular-nums text-gray-900 dark:text-white">
            {s.commitCount}
          </p>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">commits</p>
        </div>
        <div>
          <p className="text-xl font-bold tabular-nums text-gray-900 dark:text-white">
            {s.ticketCount}
          </p>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">tickets</p>
        </div>
        <div>
          <p className={`text-xl font-bold tabular-nums ${s.breakingCount > 0 ? 'text-red-600 dark:text-red-400' : 'text-gray-900 dark:text-white'}`}>
            {s.breakingCount}
          </p>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">breaking</p>
        </div>
        <div>
          <p className="text-xl font-bold tabular-nums text-gray-900 dark:text-white">
            {s.contributorCount}
          </p>
          <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">contributors</p>
        </div>
      </div>
    </Link>
  )
}


export function ProjectDashboard() {
  const { id } = useParams<{ id: string }>()

  const { data: project } = useQuery<ProjectDetailDto>({
    queryKey: ['project', id],
    queryFn: () => apiFetch(`/api/v1/projects/${id}`).then((r) => r.json()),
    enabled: !!id,
  })

  const { data: changes, isLoading, isError } = useQuery<ProjectChangesDto>({
    queryKey: ['project-changes', id],
    queryFn: () => apiFetch(`/api/v1/projects/${id}/changes`).then((r) => r.json()),
    enabled: !!id,
  })

  if (isLoading) {
    return (
      <div className="p-8">
        <p className="text-sm text-gray-500">Loading project changes…</p>
      </div>
    )
  }

  if (isError || !changes) {
    return (
      <div className="p-8">
        <p className="text-sm text-red-500">Failed to load project changes.</p>
      </div>
    )
  }

  const aggregate = changes.summary
  const projectColor = project?.color ?? '#6B7280'

  return (
    <div className="max-w-6xl space-y-6 p-6">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
        <Link to="/projects" className="hover:text-gray-700 dark:hover:text-gray-200">
          Projects
        </Link>
        <span>/</span>
        <span className="text-gray-900 dark:text-white font-medium">{changes.projectName}</span>
      </nav>

      {/* Header */}
      <div className="flex items-center justify-between gap-3 flex-wrap">
        <div className="flex items-center gap-3">
          <span
            className="inline-block w-3.5 h-3.5 rounded-full shrink-0"
            style={{ backgroundColor: projectColor }}
          />
          <h1 className="text-xl font-semibold text-gray-900 dark:text-white">
            {changes.projectName}
          </h1>
          {project?.description && (
            <span className="text-sm text-gray-400">— {project.description}</span>
          )}
        </div>
        <Link
          to={`/projects/${id}/releases/new`}
          className="px-4 py-2 rounded-md bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 transition-colors shrink-0"
        >
          New release
        </Link>
      </div>

      {/* Aggregate metrics */}
      <div>
        <h2 className="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 mb-3">
          Unreleased changes across all repositories
        </h2>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
          <MetricCard label="Total commits" value={aggregate.commitCount} />
          <MetricCard label="Unique tickets" value={aggregate.ticketCount} />
          <MetricCard
            label="Breaking changes"
            value={aggregate.breakingCount}
            accent={aggregate.breakingCount > 0}
          />
          <MetricCard label="Contributors" value={aggregate.contributorCount} />
        </div>
      </div>

      {/* Per-repository cards */}
      {changes.repositories.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 dark:border-gray-600 p-8 text-center">
          <p className="text-sm text-gray-500">No repositories assigned to this project.</p>
        </div>
      ) : (
        <div>
          <h2 className="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 mb-3">
            Repositories ({changes.repositories.length})
          </h2>
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            {changes.repositories.map((repo) => (
              <RepoCard key={repo.repositoryId} repo={repo} color={projectColor} />
            ))}
          </div>
        </div>
      )}
    </div>
  )
}
