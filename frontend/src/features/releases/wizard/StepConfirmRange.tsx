import type { components } from '../../../lib/api'

type RepositoryChangesDto = components['schemas']['RepositoryChangesDto']

interface RepositoryRangeRow {
  repositoryId: string
  repositoryName: string
  fromTag: string | null
  toTag: string
  commitCount: number
}

interface StepConfirmRangeProps {
  version: string
  onVersionChange: (v: string) => void
  ranges: RepositoryRangeRow[]
  onNext: () => void
}

export function buildSemverSuggestion(
  repos: RepositoryChangesDto[],
  currentVersions: (string | null)[],
): string {
  const hasBreaking = repos.some((r) => r.summary.breakingCount > 0)
  const hasFeat = repos.some((r) => r.groups.some((g) => g.type === 'feat' && !g.isBreaking))

  const base = findLatestSemver(currentVersions)
  const [major, minor, patch] = (base ?? '0.0.0').replace(/^v/, '').split('.').map(Number)

  if (hasBreaking) return `${major + 1}.0.0`
  if (hasFeat) return `${major}.${minor + 1}.0`
  return `${major}.${minor}.${patch + 1}`
}

function findLatestSemver(tags: (string | null)[]): string | null {
  const semverRe = /^v?(\d+)\.(\d+)\.(\d+)/
  let best: [number, number, number] | null = null
  for (const tag of tags) {
    if (!tag) continue
    const m = semverRe.exec(tag)
    if (!m) continue
    const t: [number, number, number] = [+m[1], +m[2], +m[3]]
    if (!best || t[0] > best[0] || (t[0] === best[0] && t[1] > best[1]) || (t[0] === best[0] && t[1] === best[1] && t[2] > best[2])) {
      best = t
    }
  }
  return best ? `${best[0]}.${best[1]}.${best[2]}` : null
}

export function StepConfirmRange({ version, onVersionChange, ranges, onNext }: StepConfirmRangeProps) {
  const isValid = version.trim().length > 0

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-base font-semibold text-gray-900 dark:text-white">Step 1 — Confirm version &amp; change range</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Review the commit range for each repository and confirm the release version.
        </p>
      </div>

      {/* Version input */}
      <div className="space-y-1.5">
        <label htmlFor="version" className="block text-sm font-medium text-gray-700 dark:text-gray-300">
          Version
        </label>
        <input
          id="version"
          type="text"
          value={version}
          onChange={(e) => onVersionChange(e.target.value)}
          placeholder="e.g. 1.3.0"
          className="w-full max-w-xs rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-900 dark:text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
        <p className="text-xs text-gray-400 dark:text-gray-500">
          Pre-populated from semver bump heuristic (breaking → major, feat → minor, otherwise patch).
        </p>
      </div>

      {/* Repository change range table */}
      <div>
        <h3 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
          Change ranges ({ranges.length} {ranges.length === 1 ? 'repository' : 'repositories'})
        </h3>
        {ranges.length === 0 ? (
          <p className="text-sm text-gray-500 dark:text-gray-400">No repositories with unreleased commits.</p>
        ) : (
          <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
            <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
              <thead className="bg-gray-50 dark:bg-gray-800">
                <tr>
                  <th className="px-4 py-2.5 text-left font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider text-xs">Repository</th>
                  <th className="px-4 py-2.5 text-left font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider text-xs">From tag</th>
                  <th className="px-4 py-2.5 text-left font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider text-xs">To</th>
                  <th className="px-4 py-2.5 text-right font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider text-xs">Commits</th>
                </tr>
              </thead>
              <tbody className="bg-white dark:bg-gray-900 divide-y divide-gray-100 dark:divide-gray-800">
                {ranges.map((r) => (
                  <tr key={r.repositoryId}>
                    <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">{r.repositoryName}</td>
                    <td className="px-4 py-3 font-mono text-xs text-gray-500 dark:text-gray-400">
                      {r.fromTag ?? <span className="italic text-gray-400">beginning</span>}
                    </td>
                    <td className="px-4 py-3 font-mono text-xs text-gray-500 dark:text-gray-400">{r.toTag}</td>
                    <td className="px-4 py-3 text-right tabular-nums text-gray-700 dark:text-gray-300">{r.commitCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      <div className="flex justify-end">
        <button
          onClick={onNext}
          disabled={!isValid}
          className="px-5 py-2 rounded-md bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
        >
          Next: Select template
        </button>
      </div>
    </div>
  )
}
