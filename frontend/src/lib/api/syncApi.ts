import { apiFetch } from '../apiClient'
import type { components } from '../api'

export type RepositorySyncDto = components['schemas']['RepositorySyncDto']

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
