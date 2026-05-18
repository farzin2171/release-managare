interface HealthPillProps {
  matchRate: number
  health: 'Green' | 'Amber' | 'Red' | 'Unknown'
}

const colorMap: Record<string, string> = {
  Green: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
  Amber: 'bg-amber-100 text-amber-800 dark:bg-amber-900 dark:text-amber-200',
  Red: 'bg-red-100 text-red-800 dark:bg-red-900 dark:text-red-200',
  Unknown: 'bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-300',
}

const labelMap: Record<string, string> = {
  Green: 'Healthy',
  Amber: 'At risk',
  Red: 'Critical',
  Unknown: 'Unknown',
}

export function HealthPill({ matchRate, health }: HealthPillProps) {
  const colorClass = colorMap[health] ?? colorMap.Unknown
  const label = health !== 'Unknown'
    ? `${labelMap[health]} ${Math.round(matchRate * 100)}%`
    : labelMap.Unknown

  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${colorClass}`}>
      {label}
    </span>
  )
}
