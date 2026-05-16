import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { getLatestRepoSync, triggerRepoSync } from '../../../lib/api/syncApi'
import type { RepositorySyncDto } from '../../../lib/api/syncApi'

const TERMINAL_STATUSES: RepositorySyncDto['status'][] = ['Succeeded', 'Failed', 'Skipped']

export function useRepositorySync(repositoryId: string) {
  const queryClient = useQueryClient()

  const latestSyncQuery = useQuery({
    queryKey: ['repo-sync', 'latest', repositoryId],
    queryFn: () => getLatestRepoSync(repositoryId),
    refetchInterval: (query) => {
      const status = query.state.data?.status
      if (!status || TERMINAL_STATUSES.includes(status)) return false
      return 2000
    },
    enabled: !!repositoryId,
  })

  const triggerMutation = useMutation({
    mutationFn: () => triggerRepoSync(repositoryId),
    onSuccess: (data) => {
      queryClient.setQueryData(['repo-sync', 'latest', repositoryId], data)
    },
  })

  return {
    latestSync: latestSyncQuery.data ?? null,
    isSyncLoading: latestSyncQuery.isLoading,
    trigger: triggerMutation.mutate,
    isTriggerPending: triggerMutation.isPending,
    triggerError: triggerMutation.error,
  }
}
