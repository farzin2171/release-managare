import { apiFetch } from '../apiClient'
import type { components } from '../api'

export type RepositorySyncDto = components['schemas']['RepositorySyncDto']
export type ProjectSyncDto = components['schemas']['ProjectSyncDto']
export type RepoSyncSnapshotItemDto = components['schemas']['RepoSyncSnapshotItemDto']

export async function triggerRepoSync(repositoryId: string): Promise<RepositorySyncDto> {
  const resp = await apiFetch(`/api/v1/repositories/${repositoryId}/sync`, { method: 'POST' })
  if (!resp.ok) throw new Error(`Failed to trigger sync: ${resp.status}`)
  return resp.json()
}

export async function getLatestRepoSync(repositoryId: string): Promise<RepositorySyncDto | null> {
  const resp = await apiFetch(`/api/v1/repositories/${repositoryId}/sync/latest`)
  if (resp.status === 404) return null
  if (!resp.ok) throw new Error(`Failed to fetch latest sync: ${resp.status}`)
  return resp.json()
}

export async function getRepoSyncById(syncId: string): Promise<RepositorySyncDto | null> {
  const resp = await apiFetch(`/api/v1/repository-syncs/${syncId}`)
  if (resp.status === 404) return null
  if (!resp.ok) throw new Error(`Failed to fetch sync: ${resp.status}`)
  return resp.json()
}

export async function triggerProjectSync(projectId: string): Promise<ProjectSyncDto> {
  const resp = await apiFetch(`/api/v1/projects/${projectId}/sync`, { method: 'POST' })
  if (!resp.ok) throw new Error(`Failed to trigger project sync: ${resp.status}`)
  return resp.json()
}

export async function cancelProjectSync(projectId: string): Promise<void> {
  const resp = await apiFetch(`/api/v1/projects/${projectId}/sync/active`, { method: 'DELETE' })
  if (!resp.ok) throw new Error(`Failed to cancel project sync: ${resp.status}`)
}

export async function getLatestProjectSync(projectId: string): Promise<ProjectSyncDto | null> {
  const resp = await apiFetch(`/api/v1/projects/${projectId}/sync/latest`)
  if (resp.status === 404) return null
  if (!resp.ok) throw new Error(`Failed to fetch latest project sync: ${resp.status}`)
  return resp.json()
}

export async function getActiveProjectSync(projectId: string): Promise<ProjectSyncDto | null> {
  const resp = await apiFetch(`/api/v1/projects/${projectId}/sync/active`)
  if (resp.status === 204) return null
  if (!resp.ok) throw new Error(`Failed to fetch active project sync: ${resp.status}`)
  return resp.json()
}

export async function getProjectSyncSnapshot(projectId: string): Promise<RepoSyncSnapshotItemDto[]> {
  const resp = await apiFetch(`/api/v1/projects/${projectId}/repositories/sync-snapshot`)
  if (!resp.ok) throw new Error(`Failed to fetch sync snapshot: ${resp.status}`)
  return resp.json()
}
