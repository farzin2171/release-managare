import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '../../../../lib/apiClient'
import type { components } from '../../../../lib/api'

type ProjectCustomVariableDto = components['schemas']['ProjectCustomVariableDto']

const VARS_KEY = (projectId: string) => ['project-custom-vars', projectId]

export function useProjectCustomVariables(projectId: string) {
  return useQuery<ProjectCustomVariableDto[]>({
    queryKey: VARS_KEY(projectId),
    queryFn: () =>
      apiFetch(`/api/v1/projects/${projectId}/custom-variables`).then((r) => r.json()),
    enabled: !!projectId,
  })
}

export function useUpsertCustomVariable(projectId: string) {
  const qc = useQueryClient()
  return useMutation<ProjectCustomVariableDto, Error, { key: string; value: string }>({
    mutationFn: ({ key, value }) =>
      apiFetch(`/api/v1/projects/${projectId}/custom-variables/${encodeURIComponent(key)}`, {
        method: 'PUT',
        body: JSON.stringify({ value }),
      }).then((r) => r.json()),
    onSuccess: () => qc.invalidateQueries({ queryKey: VARS_KEY(projectId) }),
  })
}

export function useDeleteCustomVariable(projectId: string) {
  const qc = useQueryClient()
  return useMutation<void, Error, string>({
    mutationFn: (key) =>
      apiFetch(`/api/v1/projects/${projectId}/custom-variables/${encodeURIComponent(key)}`, {
        method: 'DELETE',
      }).then(() => undefined),
    onSuccess: () => qc.invalidateQueries({ queryKey: VARS_KEY(projectId) }),
  })
}
