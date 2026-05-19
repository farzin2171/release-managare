import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'

type RepoJiraComparisonDto = components['schemas']['RepoJiraComparisonDto']
type ProjectJiraCoverageDto = components['schemas']['ProjectJiraCoverageDto']
type AddToFixVersionResultDto = components['schemas']['AddToFixVersionResultDto']

export function useRepoCoverage(repositoryId: string, refresh = false) {
  return useQuery<RepoJiraComparisonDto>({
    queryKey: ['jira-coverage', 'repo', repositoryId, refresh],
    queryFn: () =>
      apiFetch(`/api/v1/repositories/${repositoryId}/jira-coverage?refresh=${refresh}`).then(
        (r) => r.json()
      ),
    enabled: !!repositoryId,
    staleTime: 4 * 60 * 1000,
  })
}

export function useProjectCoverage(projectId: string, refresh = false) {
  return useQuery<ProjectJiraCoverageDto>({
    queryKey: ['jira-coverage', 'project', projectId, refresh],
    queryFn: () =>
      apiFetch(`/api/v1/projects/${projectId}/jira-coverage?refresh=${refresh}`).then(
        (r) => r.json()
      ),
    enabled: !!projectId,
    staleTime: 4 * 60 * 1000,
  })
}

export function useInvalidateRepoCoverage() {
  const queryClient = useQueryClient()
  return (repositoryId: string) =>
    queryClient.invalidateQueries({ queryKey: ['jira-coverage', 'repo', repositoryId] })
}

export function useInvalidateProjectCoverage() {
  const queryClient = useQueryClient()
  return (projectId: string) =>
    queryClient.invalidateQueries({ queryKey: ['jira-coverage', 'project', projectId] })
}

export function useAddTicketToFixVersion(repositoryId: string) {
  const queryClient = useQueryClient()
  return useMutation<AddToFixVersionResultDto, Error, string>({
    mutationFn: (ticketKey) =>
      apiFetch(`/api/v1/repositories/${repositoryId}/jira-coverage/add-ticket`, {
        method: 'POST',
        body: JSON.stringify({ ticketKey }),
      }).then(async (r) => {
        if (!r.ok) throw new Error(`Failed to add ticket: ${r.status}`)
        return r.json() as Promise<AddToFixVersionResultDto>
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['jira-coverage', 'repo', repositoryId] })
      queryClient.invalidateQueries({ queryKey: ['jira-coverage', 'project'] })
    },
  })
}
