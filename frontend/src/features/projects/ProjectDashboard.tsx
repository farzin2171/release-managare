import { useParams, Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '../../lib/apiClient'
import { useAuthStore } from '../../lib/authStore'
import { ProjectRepositoriesTable } from './components/ProjectRepositoriesTable'
import { RepositoryCard } from './components/RepositoryCard'
import type { components } from '../../lib/api'

type ProjectDetailDto = components['schemas']['ProjectDetailDto']
type ProjectChangesDto = components['schemas']['ProjectChangesDto']
type RepositoryDto = components['schemas']['RepositoryDto']

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



export function ProjectDashboard() {
  const { id } = useParams<{ id: string }>()
  const isAdmin = useAuthStore((s) => s.role === 'Admin')

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

  const { data: allRepos = [] } = useQuery<RepositoryDto[]>({
    queryKey: ['repositories'],
    queryFn: () => apiFetch('/api/v1/repositories').then((r) => r.json()),
    enabled: !!project,
  })

  const projectRepos = allRepos.filter((r) =>
    project?.repositories.some((pr) => pr.repositoryId === r.id)
  )

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
        {isAdmin ? (
          <Link
            to={`/projects/${id}/releases/new`}
            className="px-4 py-2 rounded-md bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 transition-colors shrink-0"
          >
            New release
          </Link>
        ) : (
          <span
            title="Admin access required"
            className="px-4 py-2 rounded-md bg-blue-600 text-white text-sm font-medium opacity-40 cursor-not-allowed shrink-0 select-none"
          >
            New release
          </span>
        )}
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
            {changes.repositories.map((repo) => {
              const repoMeta = allRepos.find((r) => r.id === repo.repositoryId)
              return (
                <RepositoryCard
                  key={repo.repositoryId}
                  repo={repo}
                  color={projectColor}
                  latestTag={repoMeta?.latestTag ?? null}
                />
              )
            })}
          </div>
        </div>
      )}

      {/* Repository latest tags */}
      {projectRepos.length > 0 && (
        <div>
          <h2 className="text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 mb-3">
            Latest tags
          </h2>
          <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4">
            <ProjectRepositoriesTable repositories={projectRepos} />
          </div>
        </div>
      )}
    </div>
  )
}
