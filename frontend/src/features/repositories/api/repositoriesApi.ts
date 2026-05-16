import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'

type RepositoryTagDto = components['schemas']['RepositoryTagDto']
type RepositoryDto = components['schemas']['RepositoryDto']

export async function getRepositoryTags(id: string): Promise<RepositoryTagDto[]> {
  const resp = await apiFetch(`/api/v1/repositories/${id}/tags`)
  if (!resp.ok) throw new Error(`Failed to fetch tags: ${resp.status}`)
  const body = await resp.json() as { tags: RepositoryTagDto[] }
  return body.tags
}

export async function setLatestTag(id: string, tagName: string): Promise<RepositoryDto> {
  const resp = await apiFetch(`/api/v1/repositories/${id}/latest-tag`, {
    method: 'PUT',
    body: JSON.stringify({ tagName }),
  })
  if (!resp.ok) throw new Error(`Failed to set latest tag: ${resp.status}`)
  return resp.json() as Promise<RepositoryDto>
}

export async function clearLatestTag(id: string): Promise<void> {
  const resp = await apiFetch(`/api/v1/repositories/${id}/latest-tag`, {
    method: 'DELETE',
  })
  if (!resp.ok) throw new Error(`Failed to clear latest tag: ${resp.status}`)
}
