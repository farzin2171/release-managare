import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '../../../../lib/apiClient'
import type { components } from '../../../../lib/api'

type ProjectTemplateBindingDto = components['schemas']['ProjectTemplateBindingDto']
type CreateBindingRequest = components['schemas']['CreateBindingRequest']
type UpdateBindingRequest = components['schemas']['UpdateBindingRequest']

const BINDINGS_KEY = (projectId: string) => ['project-bindings', projectId]

export function useProjectBindings(projectId: string) {
  return useQuery<ProjectTemplateBindingDto[]>({
    queryKey: BINDINGS_KEY(projectId),
    queryFn: () =>
      apiFetch(`/api/v1/projects/${projectId}/template-bindings`)
        .then((r) => r.json()),
    enabled: !!projectId,
  })
}

export function useCreateBinding(projectId: string) {
  const qc = useQueryClient()
  return useMutation<ProjectTemplateBindingDto, Error, CreateBindingRequest>({
    mutationFn: (req) =>
      apiFetch(`/api/v1/projects/${projectId}/template-bindings`, {
        method: 'POST',
        body: JSON.stringify(req),
      }).then((r) => r.json()),
    onSuccess: () => qc.invalidateQueries({ queryKey: BINDINGS_KEY(projectId) }),
  })
}

export function useUpdateBinding(projectId: string) {
  const qc = useQueryClient()
  return useMutation<
    ProjectTemplateBindingDto,
    Error,
    { bindingId: string; req: UpdateBindingRequest }
  >({
    mutationFn: ({ bindingId, req }) =>
      apiFetch(`/api/v1/projects/${projectId}/template-bindings/${bindingId}`, {
        method: 'PUT',
        body: JSON.stringify(req),
      }).then((r) => r.json()),
    onSuccess: () => qc.invalidateQueries({ queryKey: BINDINGS_KEY(projectId) }),
  })
}

export function useDeleteBinding(projectId: string) {
  const qc = useQueryClient()
  return useMutation<void, Error, string>({
    mutationFn: (bindingId) =>
      apiFetch(`/api/v1/projects/${projectId}/template-bindings/${bindingId}`, {
        method: 'DELETE',
      }).then(() => undefined),
    onSuccess: () => qc.invalidateQueries({ queryKey: BINDINGS_KEY(projectId) }),
  })
}

export function useReorderBindings(projectId: string) {
  const qc = useQueryClient()
  return useMutation<ProjectTemplateBindingDto[], Error, string[]>({
    mutationFn: (orderedIds) =>
      apiFetch(`/api/v1/projects/${projectId}/template-bindings/reorder`, {
        method: 'POST',
        body: JSON.stringify({ orderedIds }),
      }).then((r) => r.json()),
    onSuccess: () => qc.invalidateQueries({ queryKey: BINDINGS_KEY(projectId) }),
  })
}
