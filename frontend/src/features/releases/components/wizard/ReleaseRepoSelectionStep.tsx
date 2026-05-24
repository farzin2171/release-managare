import { useState, useEffect } from 'react'
import { useQuery } from '@tanstack/react-query'
import { apiFetch } from '../../../../lib/apiClient'

interface ReleasePreviewRepo {
  repositoryId: string
  name: string
  isPrimary: boolean
  hasChanges: boolean
  previousVersion: string
  suggestedNextVersion: string
  bumpType: string
  commitCount: number
  ticketCount: number
}

interface ReleasePreviewDto {
  repositories: ReleasePreviewRepo[]
  derivedReleaseVersion: string
  derivedFromRepositoryId: string
}

export interface RepoSelection {
  repositoryId: string
  nextVersion: string
  bumpType: BumpType
}

type BumpType = 'major' | 'minor' | 'patch' | 'manual'

const SEMVER_RE = /^\d+\.\d+\.\d+$/

function bumpVersion(base: string, bump: BumpType): string {
  if (!SEMVER_RE.test(base)) return ''
  const [major, minor, patch] = base.split('.').map(Number)
  switch (bump) {
    case 'major': return `${major + 1}.0.0`
    case 'minor': return `${major}.${minor + 1}.0`
    case 'patch': return `${major}.${minor}.${patch + 1}`
    default: return base
  }
}

function isGreaterThan(next: string, prev: string): boolean {
  if (!SEMVER_RE.test(next) || !prev) return SEMVER_RE.test(next)
  const [mA, miA, pA] = next.split('.').map(Number)
  const [mB, miB, pB] = prev.split('.').map(Number)
  if (mA !== mB) return mA > mB
  if (miA !== miB) return miA > miB
  return pA > pB
}

interface Props {
  projectId: string
  repoIds: string[]
  onSubmit: (name: string, selections: RepoSelection[]) => void
  onBack?: () => void
  isSubmitting?: boolean
}

export function ReleaseRepoSelectionStep({ projectId, repoIds, onSubmit, onBack, isSubmitting }: Props) {
  const [releaseName, setReleaseName] = useState('')
  const [selected, setSelected] = useState<Set<string>>(new Set(repoIds))
  const [versions, setVersions] = useState<Record<string, string>>({})
  const [bumpTypes, setBumpTypes] = useState<Record<string, BumpType>>({})
  const [errors, setErrors] = useState<Record<string, string>>({})

  const { data: preview, isLoading } = useQuery<ReleasePreviewDto>({
    queryKey: ['release-preview', projectId, repoIds],
    queryFn: () =>
      apiFetch(`/api/v1/projects/${projectId}/releases/preview`, {
        method: 'POST',
        body: JSON.stringify({ repositoryIds: repoIds }),
      }).then((r) => r.json()),
    enabled: repoIds.length > 0,
  })

  // Pre-fill versions and bump types from preview
  useEffect(() => {
    if (!preview) return
    const vInit: Record<string, string> = {}
    const bInit: Record<string, BumpType> = {}
    for (const repo of preview.repositories) {
      vInit[repo.repositoryId] = repo.suggestedNextVersion
      bInit[repo.repositoryId] = (repo.bumpType as BumpType) || 'patch'
    }
    setVersions(vInit)
    setBumpTypes(bInit)
  }, [preview])

  const toggleRepo = (id: string) =>
    setSelected((prev) => {
      const next = new Set(prev)
      next.has(id) ? next.delete(id) : next.add(id)
      return next
    })

  const handleBumpChange = (repoId: string, bump: BumpType, prevVersion: string) => {
    setBumpTypes((b) => ({ ...b, [repoId]: bump }))
    if (bump !== 'manual' && prevVersion) {
      const computed = bumpVersion(prevVersion, bump)
      setVersions((v) => ({ ...v, [repoId]: computed }))
    }
  }

  const handleVersionChange = (repoId: string, val: string) => {
    setVersions((v) => ({ ...v, [repoId]: val }))
    setBumpTypes((b) => ({ ...b, [repoId]: 'manual' }))
  }

  const derivedVersion = preview?.repositories
    .filter((r) => selected.has(r.repositoryId))
    .find((r) => r.repositoryId === preview.derivedFromRepositoryId)
    ?.repositoryId
    ? versions[preview.derivedFromRepositoryId] ?? ''
    : Object.values(versions)[0] ?? ''

  const validate = (): boolean => {
    const e: Record<string, string> = {}
    if (!releaseName.trim()) e['name'] = 'Release name is required.'
    if (selected.size === 0) e['repos'] = 'Select at least one repository.'

    for (const id of selected) {
      const ver = versions[id] ?? ''
      const prev = preview?.repositories.find((r) => r.repositoryId === id)?.previousVersion ?? ''
      if (!SEMVER_RE.test(ver)) {
        e[`ver_${id}`] = 'Must be valid semver (e.g. 1.2.3).'
      } else if (!isGreaterThan(ver, prev)) {
        e[`ver_${id}`] = `Must be greater than current version (${prev || 'none'}).`
      }
    }
    setErrors(e)
    return Object.keys(e).length === 0
  }

  const handleSubmit = () => {
    if (!validate()) return
    const selections: RepoSelection[] = Array.from(selected).map((id) => ({
      repositoryId: id,
      nextVersion: versions[id] ?? '',
      bumpType: bumpTypes[id] ?? 'manual',
    }))
    onSubmit(releaseName.trim(), selections)
  }

  if (isLoading) {
    return <p className="text-sm text-gray-500">Loading repository suggestions…</p>
  }

  const repos = preview?.repositories ?? repoIds.map((id) => ({
    repositoryId: id,
    name: id,
    isPrimary: false,
    hasChanges: false,
    previousVersion: '',
    suggestedNextVersion: '',
    bumpType: 'patch' as BumpType,
    commitCount: 0,
    ticketCount: 0,
  }))

  return (
    <div className="space-y-6">
      {/* Release name */}
      <div>
        <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
          Release name <span className="text-red-500">*</span>
        </label>
        <input
          type="text"
          value={releaseName}
          onChange={(e) => setReleaseName(e.target.value)}
          placeholder="e.g. May 2026 Release"
          className="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-900 text-sm px-3 py-2 focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
        {errors['name'] && <p className="mt-1 text-xs text-red-500">{errors['name']}</p>}
      </div>

      {/* Derived release version label */}
      {derivedVersion && (
        <div className="flex items-center gap-2 rounded-md bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 px-3 py-2 text-sm">
          <span className="font-medium text-blue-800 dark:text-blue-300">Derived release version:</span>
          <span className="font-mono text-blue-700 dark:text-blue-400">{derivedVersion}</span>
          {preview?.repositories.find((r) => r.repositoryId === preview.derivedFromRepositoryId) && (
            <span className="text-blue-600 dark:text-blue-500 text-xs">
              (from {preview.repositories.find((r) => r.repositoryId === preview.derivedFromRepositoryId)?.name})
            </span>
          )}
        </div>
      )}

      {/* Repo selection error */}
      {errors['repos'] && <p className="text-xs text-red-500">{errors['repos']}</p>}

      {/* Repository table */}
      <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
        <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
          <thead className="bg-gray-50 dark:bg-gray-800">
            <tr>
              <th className="w-8 px-3 py-2.5" />
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Repository</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Current</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Bump type</th>
              <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Next version</th>
              <th className="px-4 py-2.5 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Commits</th>
            </tr>
          </thead>
          <tbody className="bg-white dark:bg-gray-900 divide-y divide-gray-100 dark:divide-gray-800">
            {repos.map((repo) => {
              const isChecked = selected.has(repo.repositoryId)
              const verErr = errors[`ver_${repo.repositoryId}`]
              return (
                <tr key={repo.repositoryId} className={isChecked ? '' : 'opacity-50'}>
                  <td className="px-3 py-3">
                    <input
                      type="checkbox"
                      checked={isChecked}
                      onChange={() => toggleRepo(repo.repositoryId)}
                      className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                    />
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex items-center gap-2">
                      <span className="font-medium text-gray-900 dark:text-white">{repo.name}</span>
                      {repo.isPrimary && (
                        <span className="rounded-full bg-blue-100 dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 text-xs px-1.5 py-0.5">primary</span>
                      )}
                      {repo.hasChanges && (
                        <span className="rounded-full bg-yellow-100 dark:bg-yellow-900/30 text-yellow-700 dark:text-yellow-300 text-xs px-1.5 py-0.5">changes</span>
                      )}
                    </div>
                  </td>
                  <td className="px-4 py-3 font-mono text-xs text-gray-500 dark:text-gray-400">
                    {repo.previousVersion || <span className="italic text-gray-400">no tag</span>}
                  </td>
                  <td className="px-4 py-3">
                    <div className="flex gap-1 flex-wrap">
                      {(['major', 'minor', 'patch', 'manual'] as BumpType[]).map((b) => (
                        <label key={b} className="flex items-center gap-1 cursor-pointer">
                          <input
                            type="radio"
                            name={`bump-${repo.repositoryId}`}
                            value={b}
                            checked={bumpTypes[repo.repositoryId] === b}
                            onChange={() => handleBumpChange(repo.repositoryId, b, repo.previousVersion)}
                            disabled={!isChecked}
                            className="h-3 w-3 text-blue-600 focus:ring-blue-500"
                          />
                          <span className="text-xs text-gray-700 dark:text-gray-300">{b}</span>
                        </label>
                      ))}
                    </div>
                  </td>
                  <td className="px-4 py-3">
                    <input
                      type="text"
                      value={versions[repo.repositoryId] ?? ''}
                      onChange={(e) => handleVersionChange(repo.repositoryId, e.target.value)}
                      disabled={!isChecked}
                      placeholder="1.2.3"
                      className={`w-28 rounded border px-2 py-1 text-xs font-mono focus:outline-none focus:ring-1 focus:ring-blue-500 dark:bg-gray-800 ${
                        verErr ? 'border-red-400' : 'border-gray-300 dark:border-gray-600'
                      }`}
                    />
                    {verErr && <p className="mt-0.5 text-xs text-red-500">{verErr}</p>}
                  </td>
                  <td className="px-4 py-3 text-right tabular-nums text-gray-600 dark:text-gray-400 text-xs">
                    {repo.commitCount}
                    {repo.ticketCount > 0 && (
                      <span className="ml-1 text-gray-400">/ {repo.ticketCount} tickets</span>
                    )}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {/* Actions */}
      <div className="flex items-center justify-between pt-2">
        {onBack && (
          <button
            type="button"
            onClick={onBack}
            className="px-4 py-2 rounded-md border border-gray-300 dark:border-gray-600 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
          >
            Back
          </button>
        )}
        <button
          type="button"
          onClick={handleSubmit}
          disabled={isSubmitting}
          className="ml-auto px-4 py-2 rounded-md bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-40 transition-colors"
        >
          {isSubmitting ? 'Creating release…' : 'Create release'}
        </button>
      </div>
    </div>
  )
}
