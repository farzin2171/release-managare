import { HealthPill } from './HealthPill'
import type { components } from '../../../lib/api'

type ProjectJiraCoverageDto = components['schemas']['ProjectJiraCoverageDto']

function deriveProjectHealth(dto: ProjectJiraCoverageDto): 'Green' | 'Amber' | 'Red' | 'Unknown' {
  if (dto.totalRepoCount === 0) return 'Unknown'
  const rate = dto.projectMatchRate
  if (rate >= 0.9) return 'Green'
  if (rate >= 0.6) return 'Amber'
  return 'Red'
}

interface ProjectCoverageAggregateProps {
  coverage: ProjectJiraCoverageDto
}

function Metric({ label, value, accent }: { label: string; value: string | number; accent?: string }) {
  return (
    <div className="text-center">
      <p className={`text-2xl font-bold tabular-nums ${accent ?? 'text-gray-900 dark:text-white'}`}>
        {value}
      </p>
      <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">{label}</p>
    </div>
  )
}

export function ProjectCoverageAggregate({ coverage: cov }: ProjectCoverageAggregateProps) {
  const health = deriveProjectHealth(cov)

  return (
    <div className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 px-5 py-4">
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div className="flex items-center gap-2">
          <h2 className="text-sm font-medium text-gray-700 dark:text-gray-300">
            Jira coverage
          </h2>
          <HealthPill matchRate={cov.projectMatchRate} health={health} />
        </div>
        <div className="flex items-center gap-6">
          <Metric label="Total repos" value={cov.totalRepoCount} />
          <Metric
            label="Healthy"
            value={cov.greenRepoCount}
            accent="text-green-600 dark:text-green-400"
          />
          <Metric
            label="Need attention"
            value={cov.attentionRepoCount}
            accent={cov.attentionRepoCount > 0 ? 'text-red-600 dark:text-red-400' : undefined}
          />
          <Metric
            label="Match rate"
            value={`${Math.round(cov.projectMatchRate * 100)}%`}
          />
        </div>
      </div>
    </div>
  )
}
