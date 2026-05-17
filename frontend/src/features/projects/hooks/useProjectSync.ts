import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  triggerProjectSync,
  cancelProjectSync,
  getLatestProjectSync,
  getActiveProjectSync,
  type ProjectSyncDto,
  type RepoSyncSnapshotItemDto,
} from '../../../lib/api/syncApi'
import { openProjectSyncStream, type RepoCompletedPayload } from '../../../lib/api/syncSse'

const ACTIVE_STATUSES: ProjectSyncDto['status'][] = ['Pending', 'InProgress']

export function useProjectSync(projectId: string) {
  const queryClient = useQueryClient()
  const [isStreaming, setIsStreaming] = useState(false)
  const stopStreamRef = useRef<(() => void) | null>(null)
  const sseErrorCount = useRef(0)

  const activeQuery = useQuery({
    queryKey: ['project-sync', 'active', projectId],
    queryFn: () => getActiveProjectSync(projectId),
    refetchInterval: (query) => {
      const status = query.state.data?.status
      if (!status || !ACTIVE_STATUSES.includes(status)) return false
      return isStreaming ? false : 3000
    },
    enabled: !!projectId,
  })

  const latestQuery = useQuery({
    queryKey: ['project-sync', 'latest', projectId],
    queryFn: () => getLatestProjectSync(projectId),
    staleTime: 5000,
    enabled: !!projectId,
  })

  const startMutation = useMutation({
    mutationFn: () => triggerProjectSync(projectId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['project-sync', 'active', projectId] })
    },
  })

  const cancelMutation = useMutation({
    mutationFn: () => cancelProjectSync(projectId),
  })

  // Open SSE stream when there's an active sync
  useEffect(() => {
    const active = activeQuery.data
    if (!active || !ACTIVE_STATUSES.includes(active.status)) {
      stopStreamRef.current?.()
      stopStreamRef.current = null
      setIsStreaming(false)
      return
    }

    if (isStreaming) return

    setIsStreaming(true)
    sseErrorCount.current = 0

    const stop = openProjectSyncStream(projectId, {
      onRepoCompleted: (payload: RepoCompletedPayload) => {
        queryClient.setQueryData<RepoSyncSnapshotItemDto[]>(
          ['project', projectId, 'sync-snapshot'],
          (old) =>
            old?.map((item) =>
              item.repositoryId === payload.repoId
                ? { ...item, currentStep: null }
                : item,
            ) ?? old,
        )
      },
      onComplete: () => {
        setIsStreaming(false)
        stopStreamRef.current = null
        void queryClient.invalidateQueries({ queryKey: ['project-sync', 'active', projectId] })
        void queryClient.invalidateQueries({ queryKey: ['project-sync', 'latest', projectId] })
        void queryClient.invalidateQueries({ queryKey: ['project', projectId, 'sync-snapshot'] })
      },
      onError: () => {
        sseErrorCount.current++
        if (sseErrorCount.current >= 2) setIsStreaming(false)
      },
    })

    stopStreamRef.current = stop

    return () => {
      stop()
      stopStreamRef.current = null
      setIsStreaming(false)
    }
  }, [activeQuery.data?.id, projectId, queryClient])

  return {
    activeSsync: activeQuery.data ?? null,
    latestSync: latestQuery.data ?? null,
    isRunning: !!activeQuery.data && ACTIVE_STATUSES.includes(activeQuery.data.status),
    isStreaming,
    start: startMutation.mutate,
    cancel: cancelMutation.mutate,
    isStarting: startMutation.isPending,
    isCancelling: cancelMutation.isPending,
    startError: startMutation.error,
  }
}
