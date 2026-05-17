import { useQuery } from '@tanstack/react-query'
import { getProjectSyncSnapshot, type RepoSyncSnapshotItemDto } from '../../../lib/api/syncApi'

export function useProjectSyncSnapshot(projectId: string) {
  return useQuery<RepoSyncSnapshotItemDto[]>({
    queryKey: ['project', projectId, 'sync-snapshot'],
    queryFn: () => getProjectSyncSnapshot(projectId),
    staleTime: 5000,
    enabled: !!projectId,
  })
}
