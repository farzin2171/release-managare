import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'

type ProjectDto = components['schemas']['ProjectDto']
type ProjectDetailDto = components['schemas']['ProjectDetailDto']
type RepositoryDto = components['schemas']['RepositoryDto']
type JiraConnectionDto = components['schemas']['JiraConnectionResponseDto']

// ─── Schemas ──────────────────────────────────────────────────────────────────

const projectSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  description: z.string().optional(),
  color: z.string().regex(/^#[0-9A-Fa-f]{6}$/, 'Must be a hex colour'),
})
type ProjectFormData = z.infer<typeof projectSchema>

const jiraSchema = z.object({
  jiraProjectKeys: z.string(),
  fixVersionPattern: z.string().optional(),
  autoCreateFixVersion: z.boolean(),
  matchSubtasksToParents: z.boolean(),
})
type JiraFormData = z.infer<typeof jiraSchema>

const confluenceSchema = z.object({
  confluenceSpaceKey: z.string().optional(),
  confluenceParentPageId: z.string().optional(),
})
type ConfluenceFormData = z.infer<typeof confluenceSchema>

// ─── Helper components ────────────────────────────────────────────────────────

function ColorSwatch({ color }: { color: string }) {
  return (
    <span
      className="inline-block w-3 h-3 rounded-full mr-2 shrink-0"
      style={{ backgroundColor: color }}
    />
  )
}

// ─── Create project modal ─────────────────────────────────────────────────────

function CreateProjectModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient()
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<ProjectFormData>({
    resolver: zodResolver(projectSchema),
    defaultValues: { color: '#3B82F6' },
  })

  const create = useMutation({
    mutationFn: async (data: ProjectFormData) => {
      const res = await apiFetch('/api/v1/projects', {
        method: 'POST',
        body: JSON.stringify(data),
      })
      if (!res.ok) throw new Error('Failed to create project')
      return res.json() as Promise<ProjectDto>
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['projects'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-md bg-white dark:bg-gray-800 rounded-xl shadow-lg p-6 space-y-4">
        <h2 className="text-base font-semibold text-gray-900 dark:text-white">New project</h2>
        <form onSubmit={handleSubmit((d) => create.mutate(d))} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Name
            </label>
            <input
              {...register('name')}
              autoFocus
              className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            {errors.name && <p className="mt-1 text-xs text-red-500">{errors.name.message}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Description
            </label>
            <textarea
              {...register('description')}
              rows={2}
              className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Badge colour
            </label>
            <input
              {...register('color')}
              type="color"
              className="h-9 w-24 rounded-md border border-gray-300 dark:border-gray-600 p-1 bg-white dark:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            {errors.color && <p className="mt-1 text-xs text-red-500">{errors.color.message}</p>}
          </div>
          {create.isError && (
            <p className="text-sm text-red-500">{(create.error as Error).message}</p>
          )}
          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-600 hover:text-gray-800 dark:text-gray-400 dark:hover:text-gray-200"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting || create.isPending}
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {isSubmitting || create.isPending ? 'Creating…' : 'Create project'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Project detail panel ─────────────────────────────────────────────────────

function ProjectDetail({ projectId }: { projectId: string }) {
  const qc = useQueryClient()
  const [assigningRepoId, setAssigningRepoId] = useState('')

  const { data: project, isLoading } = useQuery<ProjectDetailDto>({
    queryKey: ['project', projectId],
    queryFn: () => apiFetch(`/api/v1/projects/${projectId}`).then((r) => r.json()),
  })

  const { data: allRepos = [] } = useQuery<RepositoryDto[]>({
    queryKey: ['repositories', { isTracked: true }],
    queryFn: () =>
      apiFetch('/api/v1/repositories?isTracked=true').then((r) => r.json()),
  })

  const { data: jiraConnection } = useQuery<JiraConnectionDto | null>({
    queryKey: ['jira-connection'],
    queryFn: () =>
      apiFetch('/api/v1/integrations/jira').then((r) => (r.status === 404 ? null : r.json())),
  })

  // ── Project meta form
  const {
    register: regMeta,
    handleSubmit: submitMeta,
    formState: { errors: metaErrors, isSubmitting: metaSubmitting },
  } = useForm<ProjectFormData>({
    resolver: zodResolver(projectSchema),
    values: project ? { name: project.name, description: project.description ?? '', color: project.color } : undefined,
  })

  const updateMeta = useMutation({
    mutationFn: async (data: ProjectFormData) => {
      const res = await apiFetch(`/api/v1/projects/${projectId}`, {
        method: 'PUT',
        body: JSON.stringify(data),
      })
      if (!res.ok) throw new Error('Failed to update project')
      return res.json()
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['project', projectId] })
      qc.invalidateQueries({ queryKey: ['projects'] })
    },
  })

  // ── Jira form
  const {
    register: regJira,
    handleSubmit: submitJira,
    formState: { errors: jiraErrors, isSubmitting: jiraSubmitting },
  } = useForm<JiraFormData>({
    resolver: zodResolver(jiraSchema),
    values: project
      ? {
          jiraProjectKeys: (project.jiraProjectKeys ?? []).join(', '),
          fixVersionPattern: project.fixVersionPattern ?? '',
          autoCreateFixVersion: project.autoCreateFixVersion,
          matchSubtasksToParents: project.matchSubtasksToParents,
        }
      : undefined,
  })

  const saveJira = useMutation({
    mutationFn: async (data: JiraFormData) => {
      if (!jiraConnection) throw new Error('No Jira connection configured')
      const keys = data.jiraProjectKeys
        .split(/[,\s]+/)
        .map((k) => k.trim().toUpperCase())
        .filter(Boolean)
      const res = await apiFetch(`/api/v1/projects/${projectId}/jira`, {
        method: 'PUT',
        body: JSON.stringify({
          jiraConnectionId: jiraConnection.id,
          jiraProjectKeys: keys,
          fixVersionPattern: data.fixVersionPattern || null,
          autoCreateFixVersion: data.autoCreateFixVersion,
          matchSubtasksToParents: data.matchSubtasksToParents,
        }),
      })
      if (!res.ok) throw new Error('Failed to save Jira settings')
      return res.json()
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['project', projectId] }),
  })

  // ── Confluence form (stored on project detail, updated via PUT /projects/{id})
  const {
    register: regConf,
    handleSubmit: submitConf,
    formState: { errors: confErrors, isSubmitting: confSubmitting },
  } = useForm<ConfluenceFormData>({
    resolver: zodResolver(confluenceSchema),
    values: project
      ? {
          confluenceSpaceKey: project.confluenceSpaceKey ?? '',
          confluenceParentPageId: project.confluenceParentPageId ?? '',
        }
      : undefined,
  })

  const saveConf = useMutation({
    mutationFn: async (data: ConfluenceFormData) => {
      if (!project) return
      const res = await apiFetch(`/api/v1/projects/${projectId}`, {
        method: 'PUT',
        body: JSON.stringify({
          name: project.name,
          description: project.description,
          color: project.color,
          releaseNoteTemplateId: project.releaseNoteTemplateId,
          ...data,
        }),
      })
      if (!res.ok) throw new Error('Failed to save Confluence settings')
      return res.json()
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['project', projectId] }),
  })

  // ── Repository assignment
  const assignRepo = useMutation({
    mutationFn: async ({ repoId, isPrimary }: { repoId: string; isPrimary: boolean }) => {
      const res = await apiFetch(`/api/v1/projects/${projectId}/repositories/${repoId}`, {
        method: 'POST',
        body: JSON.stringify({ isPrimary }),
      })
      if (!res.ok) throw new Error('Failed to assign repository')
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['project', projectId] }),
  })

  const removeRepo = useMutation({
    mutationFn: async (repoId: string) => {
      const res = await apiFetch(`/api/v1/projects/${projectId}/repositories/${repoId}`, {
        method: 'DELETE',
      })
      if (!res.ok) throw new Error('Failed to remove repository')
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['project', projectId] }),
  })

  const setPrimary = (repoId: string) =>
    assignRepo.mutate({ repoId, isPrimary: true })

  if (isLoading || !project) {
    return <p className="text-sm text-gray-500">Loading…</p>
  }

  const assignedRepoIds = new Set(project.repositories.map((r) => r.repositoryId))
  const unassignedRepos = allRepos.filter((r) => !assignedRepoIds.has(r.id))

  return (
    <div className="space-y-8">
      {/* ── Project meta ──────────────────────────────────────────────────── */}
      <section className="rounded-lg border border-gray-200 dark:border-gray-700 p-6 space-y-4">
        <h3 className="text-sm font-semibold text-gray-900 dark:text-white">Project details</h3>
        <form onSubmit={submitMeta((d) => updateMeta.mutate(d))} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Name
              </label>
              <input
                {...regMeta('name')}
                className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              {metaErrors.name && (
                <p className="mt-1 text-xs text-red-500">{metaErrors.name.message}</p>
              )}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Badge colour
              </label>
              <input
                {...regMeta('color')}
                type="color"
                className="h-9 w-24 rounded-md border border-gray-300 dark:border-gray-600 p-1 bg-white dark:bg-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Description
            </label>
            <textarea
              {...regMeta('description')}
              rows={2}
              className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          {updateMeta.isError && (
            <p className="text-sm text-red-500">{(updateMeta.error as Error).message}</p>
          )}
          <div>
            <button
              type="submit"
              disabled={metaSubmitting || updateMeta.isPending}
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {metaSubmitting || updateMeta.isPending ? 'Saving…' : 'Save details'}
            </button>
          </div>
        </form>
      </section>

      {/* ── Jira settings ─────────────────────────────────────────────────── */}
      <section className="rounded-lg border border-gray-200 dark:border-gray-700 p-6 space-y-4">
        <h3 className="text-sm font-semibold text-gray-900 dark:text-white">Jira</h3>
        {!jiraConnection ? (
          <p className="text-sm text-gray-400">
            Configure a Jira connection in Integrations → Jira first.
          </p>
        ) : (
          <form onSubmit={submitJira((d) => saveJira.mutate(d))} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Project keys (comma-separated)
              </label>
              <input
                {...regJira('jiraProjectKeys')}
                placeholder="APPLY, CORE"
                className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              {jiraErrors.jiraProjectKeys && (
                <p className="mt-1 text-xs text-red-500">{jiraErrors.jiraProjectKeys.message}</p>
              )}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Fix version pattern
              </label>
              <input
                {...regJira('fixVersionPattern')}
                placeholder="Apply {version}"
                className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div className="space-y-2">
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  {...regJira('autoCreateFixVersion')}
                  type="checkbox"
                  className="rounded border-gray-300 dark:border-gray-600 text-blue-600 focus:ring-blue-500"
                />
                <span className="text-sm text-gray-700 dark:text-gray-300">
                  Auto-create fix version if missing
                </span>
              </label>
              <label className="flex items-center gap-2 cursor-pointer">
                <input
                  {...regJira('matchSubtasksToParents')}
                  type="checkbox"
                  className="rounded border-gray-300 dark:border-gray-600 text-blue-600 focus:ring-blue-500"
                />
                <span className="text-sm text-gray-700 dark:text-gray-300">
                  Match subtasks to parent tickets
                </span>
              </label>
            </div>
            {saveJira.isError && (
              <p className="text-sm text-red-500">{(saveJira.error as Error).message}</p>
            )}
            <div>
              <button
                type="submit"
                disabled={jiraSubmitting || saveJira.isPending}
                className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                {jiraSubmitting || saveJira.isPending ? 'Saving…' : 'Save Jira settings'}
              </button>
            </div>
          </form>
        )}
      </section>

      {/* ── Confluence settings ────────────────────────────────────────────── */}
      <section className="rounded-lg border border-gray-200 dark:border-gray-700 p-6 space-y-4">
        <h3 className="text-sm font-semibold text-gray-900 dark:text-white">Confluence</h3>
        <form onSubmit={submitConf((d) => saveConf.mutate(d))} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Space key
              </label>
              <input
                {...regConf('confluenceSpaceKey')}
                placeholder="RELEASES"
                className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Parent page ID
              </label>
              <input
                {...regConf('confluenceParentPageId')}
                placeholder="123456789"
                className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
            </div>
          </div>
          {saveConf.isError && (
            <p className="text-sm text-red-500">{(saveConf.error as Error).message}</p>
          )}
          <div>
            <button
              type="submit"
              disabled={confSubmitting || saveConf.isPending}
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {confSubmitting || saveConf.isPending ? 'Saving…' : 'Save Confluence settings'}
            </button>
          </div>
        </form>
      </section>

      {/* ── Assigned repositories ──────────────────────────────────────────── */}
      <section className="rounded-lg border border-gray-200 dark:border-gray-700 p-6 space-y-4">
        <h3 className="text-sm font-semibold text-gray-900 dark:text-white">Repositories</h3>

        {project.repositories.length === 0 ? (
          <p className="text-sm text-gray-400">No repositories assigned yet.</p>
        ) : (
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
            <thead>
              <tr>
                {['Name', 'Branch', 'Primary', ''].map((h) => (
                  <th
                    key={h}
                    className="pb-2 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 pr-4"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-700/50">
              {project.repositories.map((r) => (
                <tr key={r.repositoryId}>
                  <td className="py-2 pr-4 font-medium text-gray-900 dark:text-white">
                    {r.name}
                  </td>
                  <td className="py-2 pr-4 font-mono text-xs text-gray-500 dark:text-gray-400">
                    {r.defaultBranch}
                  </td>
                  <td className="py-2 pr-4">
                    {r.isPrimary ? (
                      <span className="inline-flex items-center rounded px-2 py-0.5 text-xs font-medium bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300">
                        Primary
                      </span>
                    ) : (
                      <button
                        onClick={() => setPrimary(r.repositoryId)}
                        className="text-xs text-gray-400 hover:text-blue-600 dark:hover:text-blue-400"
                      >
                        Set primary
                      </button>
                    )}
                  </td>
                  <td className="py-2 text-right">
                    <button
                      onClick={() => removeRepo.mutate(r.repositoryId)}
                      disabled={removeRepo.isPending}
                      className="text-xs text-red-500 hover:text-red-700 disabled:opacity-50"
                    >
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}

        {/* Assign repository */}
        {unassignedRepos.length > 0 && (
          <div className="flex gap-2 items-center pt-2 border-t border-gray-100 dark:border-gray-700/50">
            <select
              value={assigningRepoId}
              onChange={(e) => setAssigningRepoId(e.target.value)}
              className="flex-1 rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">Select repository to assign…</option>
              {unassignedRepos.map((r) => (
                <option key={r.id} value={r.id}>
                  {r.name} ({r.azureProjectName})
                </option>
              ))}
            </select>
            <button
              disabled={!assigningRepoId || assignRepo.isPending}
              onClick={() => {
                if (!assigningRepoId) return
                const isFirst = project.repositories.length === 0
                assignRepo.mutate(
                  { repoId: assigningRepoId, isPrimary: isFirst },
                  { onSuccess: () => setAssigningRepoId('') },
                )
              }}
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {assignRepo.isPending ? 'Assigning…' : 'Assign'}
            </button>
          </div>
        )}
      </section>
    </div>
  )
}

// ─── Main page ────────────────────────────────────────────────────────────────

export function ProjectsPage() {
  const qc = useQueryClient()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  const { data: projects = [], isLoading } = useQuery<ProjectDto[]>({
    queryKey: ['projects'],
    queryFn: () => apiFetch('/api/v1/projects').then((r) => r.json()),
  })

  const deleteProject = useMutation({
    mutationFn: async (id: string) => {
      const res = await apiFetch(`/api/v1/projects/${id}`, { method: 'DELETE' })
      if (!res.ok) throw new Error('Failed to delete project')
    },
    onSuccess: (_, id) => {
      qc.invalidateQueries({ queryKey: ['projects'] })
      if (selectedId === id) setSelectedId(null)
    },
    onSettled: () => setDeletingId(null),
  })

  const handleDelete = (id: string) => {
    if (!confirm('Delete this project? This cannot be undone.')) return
    setDeletingId(id)
    deleteProject.mutate(id)
  }

  return (
    <div className="flex gap-6 min-h-[calc(100vh-8rem)]">
      {/* ── Sidebar ─────────────────────────────────────────────────────── */}
      <aside className="w-56 shrink-0 space-y-1">
        <div className="flex items-center justify-between mb-3">
          <h2 className="text-sm font-semibold text-gray-900 dark:text-white">Projects</h2>
          <button
            onClick={() => setShowCreate(true)}
            className="text-xs font-medium text-blue-600 hover:text-blue-800 dark:text-blue-400"
          >
            + New
          </button>
        </div>

        {isLoading ? (
          <p className="text-sm text-gray-400">Loading…</p>
        ) : projects.length === 0 ? (
          <p className="text-sm text-gray-400">No projects yet.</p>
        ) : (
          projects.map((p) => (
            <div
              key={p.id}
              className={`group flex items-center justify-between rounded-md px-3 py-2 cursor-pointer text-sm transition-colors ${
                selectedId === p.id
                  ? 'bg-blue-50 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300'
                  : 'text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700'
              }`}
              onClick={() => setSelectedId(p.id)}
            >
              <span className="flex items-center truncate">
                <ColorSwatch color={p.color} />
                <span className="truncate">{p.name}</span>
              </span>
              <button
                onClick={(e) => {
                  e.stopPropagation()
                  handleDelete(p.id)
                }}
                disabled={deletingId === p.id}
                className="hidden group-hover:block text-gray-400 hover:text-red-500 disabled:opacity-50 ml-1 shrink-0"
                aria-label={`Delete ${p.name}`}
              >
                ×
              </button>
            </div>
          ))
        )}
      </aside>

      {/* ── Detail panel ─────────────────────────────────────────────────── */}
      <div className="flex-1 min-w-0">
        {selectedId ? (
          <ProjectDetail key={selectedId} projectId={selectedId} />
        ) : (
          <div className="flex items-center justify-center h-full rounded-lg border border-dashed border-gray-300 dark:border-gray-600 p-12 text-center">
            <div>
              <p className="text-sm text-gray-500 dark:text-gray-400">
                {projects.length === 0
                  ? 'Create your first project to get started.'
                  : 'Select a project from the sidebar to configure it.'}
              </p>
              {projects.length === 0 && (
                <button
                  onClick={() => setShowCreate(true)}
                  className="mt-4 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
                >
                  New project
                </button>
              )}
            </div>
          </div>
        )}
      </div>

      {showCreate && <CreateProjectModal onClose={() => setShowCreate(false)} />}
    </div>
  )
}
