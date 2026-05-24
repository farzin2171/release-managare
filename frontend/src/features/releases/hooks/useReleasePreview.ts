import { useMutation } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'

export interface ReleasePreviewRepo {
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

export interface ReleasePreviewDto {
  repositories: ReleasePreviewRepo[]
  derivedReleaseVersion: string
  derivedFromRepositoryId: string
}

export function useReleasePreview(projectId: string) {
  return useMutation<ReleasePreviewDto, Error, string[]>({
    mutationFn: (repositoryIds: string[]) =>
      apiFetch(`/api/v1/projects/${projectId}/releases/preview`, {
        method: 'POST',
        body: JSON.stringify({ repositoryIds }),
      }).then((r) => r.json()),
  })
}
