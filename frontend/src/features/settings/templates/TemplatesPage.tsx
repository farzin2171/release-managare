import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'
import { TemplateEditor, VARIABLE_REFERENCE } from './TemplateEditor'

type TemplateDto = components['schemas']['TemplateDto']
type ProjectDto = components['schemas']['ProjectDto']

interface TemplatePreviewDto {
  renderedTitle: string
  renderedBody: string
  unknownTokens: string[]
  contextSource: string
  projectName: string | null
  releaseVersion: string | null
}

// ─── Schemas ──────────────────────────────────────────────────────────────────

const templateSchema = z.object({
  name: z.string().min(1, 'Name is required'),
  contentTemplate: z.string().min(1, 'Template content is required'),
})
type TemplateFormData = z.infer<typeof templateSchema>

const DEFAULT_TEMPLATE = `# {{project.name}} Release {{version}}

{{#if sections.breaking.length}}
## Breaking Changes

{{#each sections.breaking}}
- **[{{key}}]** {{title}}
{{/each}}
{{/if}}

{{#if sections.features.length}}
## Features

{{#each sections.features}}
- **[{{key}}]** {{title}}
{{/each}}
{{/if}}

{{#if sections.fixes.length}}
## Bug Fixes

{{#each sections.fixes}}
- **[{{key}}]** {{title}}
{{/each}}
{{/if}}

{{#if sections.other.length}}
## Other

{{#each sections.other}}
- **[{{key}}]** {{title}}
{{/each}}
{{/if}}

## Contributors

{{#each contributors}}
- {{this}}
{{/each}}
`

// ─── Template preview panel ───────────────────────────────────────────────────

interface TemplatePreviewPanelProps {
  template: TemplateDto
  onClose: () => void
}

function TemplatePreviewPanel({ template, onClose }: TemplatePreviewPanelProps) {
  const [contextSource, setContextSource] = useState<'synthetic' | 'project'>('synthetic')
  const [selectedProjectId, setSelectedProjectId] = useState<string>('')

  const { data: projects = [] } = useQuery<ProjectDto[]>({
    queryKey: ['projects'],
    queryFn: () => apiFetch('/api/v1/projects').then((r) => r.json()),
  })

  const previewQuery = useQuery<TemplatePreviewDto>({
    queryKey: ['template-preview', template.id, contextSource, selectedProjectId],
    queryFn: () => {
      const params = new URLSearchParams({ contextSource })
      if (contextSource === 'project' && selectedProjectId) params.set('projectId', selectedProjectId)
      return apiFetch(`/api/v1/templates/${template.id}/preview?${params}`).then((r) => r.json())
    },
    enabled: contextSource === 'synthetic' || (contextSource === 'project' && !!selectedProjectId),
  })

  return (
    <div className="fixed inset-0 z-50 flex">
      <div className="flex-1 bg-black/40" onClick={onClose} />
      <div className="w-full max-w-3xl bg-white dark:bg-gray-800 shadow-xl flex flex-col overflow-hidden">
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-700">
          <h2 className="text-base font-semibold text-gray-900 dark:text-white">
            Preview: {template.name}
          </h2>
          <button onClick={onClose} className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 text-xl leading-none" aria-label="Close">×</button>
        </div>

        <div className="flex items-center gap-3 px-6 py-3 border-b border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-900/40">
          <span className="text-sm font-medium text-gray-600 dark:text-gray-400">Context:</span>
          <select
            value={contextSource}
            onChange={(e) => setContextSource(e.target.value as 'synthetic' | 'project')}
            className="rounded-md border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            <option value="synthetic">Synthetic sample</option>
            <option value="project">Latest release of project</option>
          </select>
          {contextSource === 'project' && (
            <select
              value={selectedProjectId}
              onChange={(e) => setSelectedProjectId(e.target.value)}
              className="flex-1 rounded-md border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="">Select project…</option>
              {projects.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
          )}
        </div>

        <div className="flex-1 overflow-auto px-6 py-4 space-y-4">
          {previewQuery.isLoading && (
            <p className="text-sm text-gray-400">Rendering preview…</p>
          )}
          {previewQuery.isError && (
            <p className="text-sm text-red-500">Failed to load preview.</p>
          )}
          {previewQuery.data && (
            <>
              <div>
                <p className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Page title</p>
                <p className="text-sm font-semibold text-gray-900 dark:text-white bg-gray-50 dark:bg-gray-900/40 rounded px-3 py-2">
                  {previewQuery.data.renderedTitle}
                </p>
              </div>
              {previewQuery.data.unknownTokens && previewQuery.data.unknownTokens.length > 0 && (
                <div className="rounded-md bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 px-3 py-2">
                  <p className="text-xs font-medium text-amber-800 dark:text-amber-300">Unknown tokens:</p>
                  <div className="flex flex-wrap gap-1 mt-1">
                    {previewQuery.data.unknownTokens.map((t: string) => (
                      <span key={t} className="inline-flex items-center rounded px-1.5 py-0.5 text-xs bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-400 font-mono">
                        {t}
                      </span>
                    ))}
                  </div>
                </div>
              )}
              <div>
                <p className="text-xs font-medium text-gray-500 dark:text-gray-400 uppercase tracking-wider mb-1">Body</p>
                <pre className="text-xs text-gray-700 dark:text-gray-300 whitespace-pre-wrap font-mono bg-gray-50 dark:bg-gray-900/40 rounded p-3 overflow-auto max-h-[60vh]">
                  {previewQuery.data.renderedBody}
                </pre>
              </div>
            </>
          )}
        </div>
      </div>
    </div>
  )
}

// ─── Slide-over form ──────────────────────────────────────────────────────────

interface SlideOverProps {
  template?: TemplateDto
  onClose: () => void
}

function TemplateSlideOver({ template, onClose }: SlideOverProps) {
  const qc = useQueryClient()
  const isEdit = !!template

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<TemplateFormData>({
    resolver: zodResolver(templateSchema),
    defaultValues: {
      name: template?.name ?? '',
      contentTemplate: template?.contentTemplate ?? DEFAULT_TEMPLATE,
    },
  })

  const contentValue = watch('contentTemplate')

  const save = useMutation({
    mutationFn: async (data: TemplateFormData) => {
      const url = isEdit ? `/api/v1/templates/${template!.id}` : '/api/v1/templates'
      const res = await apiFetch(url, {
        method: isEdit ? 'PUT' : 'POST',
        body: JSON.stringify(data),
      })
      if (!res.ok) throw new Error(isEdit ? 'Failed to update template' : 'Failed to create template')
      return res.json() as Promise<TemplateDto>
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['templates'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex">
      {/* Backdrop */}
      <div className="flex-1 bg-black/40" onClick={onClose} />

      {/* Panel */}
      <div className="w-full max-w-4xl bg-white dark:bg-gray-800 shadow-xl flex flex-col overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-700">
          <h2 className="text-base font-semibold text-gray-900 dark:text-white">
            {isEdit ? 'Edit template' : 'New template'}
          </h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 dark:hover:text-gray-200 text-xl leading-none"
            aria-label="Close"
          >
            ×
          </button>
        </div>

        {/* Body */}
        <form
          onSubmit={handleSubmit((d) => save.mutate(d))}
          className="flex flex-col flex-1 overflow-hidden"
        >
          <div className="flex-1 overflow-auto px-6 py-4 space-y-4">
            {/* Name */}
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Template name
              </label>
              <input
                {...register('name')}
                autoFocus
                className="w-full max-w-sm rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              {errors.name && (
                <p className="mt-1 text-xs text-red-500">{errors.name.message}</p>
              )}
            </div>

            {/* Template editor + live preview */}
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">
                Content
              </label>
              <TemplateEditor
                value={contentValue}
                onChange={(v) => setValue('contentTemplate', v, { shouldValidate: true })}
                rows={18}
              />
              {errors.contentTemplate && (
                <p className="mt-1 text-xs text-red-500">{errors.contentTemplate.message}</p>
              )}
            </div>

            {/* Variable reference */}
            <details className="group">
              <summary className="cursor-pointer text-sm font-medium text-blue-600 dark:text-blue-400 select-none list-none flex items-center gap-1">
                <span className="group-open:hidden">▸</span>
                <span className="hidden group-open:inline">▾</span>
                Available variables
              </summary>
              <div className="mt-2 overflow-auto rounded-md border border-gray-200 dark:border-gray-700">
                <table className="min-w-full text-xs divide-y divide-gray-200 dark:divide-gray-700">
                  <thead>
                    <tr>
                      <th className="px-3 py-2 text-left font-semibold text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-900/40 w-56">Variable</th>
                      <th className="px-3 py-2 text-left font-semibold text-gray-500 dark:text-gray-400 bg-gray-50 dark:bg-gray-900/40">Description</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-gray-100 dark:divide-gray-700/50">
                    {VARIABLE_REFERENCE.map((row) => (
                      <tr key={row.variable}>
                        <td className="px-3 py-1.5 font-mono text-gray-800 dark:text-gray-200 whitespace-nowrap">
                          {row.variable}
                        </td>
                        <td className="px-3 py-1.5 text-gray-600 dark:text-gray-400">
                          {row.description}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </details>
          </div>

          {/* Footer */}
          <div className="shrink-0 flex justify-end gap-3 px-6 py-4 border-t border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800">
            {save.isError && (
              <p className="flex-1 text-sm text-red-500 self-center">
                {(save.error as Error).message}
              </p>
            )}
            <button
              type="button"
              onClick={onClose}
              className="rounded-md px-4 py-2 text-sm font-medium text-gray-600 hover:text-gray-800 dark:text-gray-400 dark:hover:text-gray-200"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSubmitting || save.isPending}
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {isSubmitting || save.isPending ? 'Saving…' : isEdit ? 'Save changes' : 'Create template'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

// ─── Main page ────────────────────────────────────────────────────────────────

export function TemplatesPage() {
  const qc = useQueryClient()
  const [slideOver, setSlideOver] = useState<'new' | TemplateDto | null>(null)
  const [previewing, setPreviewing] = useState<TemplateDto | null>(null)
  const [cloneError, setCloneError] = useState<string | null>(null)

  const { data: templates = [], isLoading } = useQuery<TemplateDto[]>({
    queryKey: ['templates'],
    queryFn: () => apiFetch('/api/v1/templates').then((r) => r.json()),
  })

  const deleteTemplate = useMutation({
    mutationFn: async (id: string) => {
      const res = await apiFetch(`/api/v1/templates/${id}`, { method: 'DELETE' })
      if (!res.ok) throw new Error('Failed to delete template')
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['templates'] }),
  })

  const cloneTemplate = useMutation({
    mutationFn: async (id: string) => {
      const res = await apiFetch(`/api/v1/templates/${id}/clone`, { method: 'POST' })
      if (!res.ok) throw new Error('Failed to clone template')
      return res.json() as Promise<TemplateDto>
    },
    onSuccess: () => {
      setCloneError(null)
      qc.invalidateQueries({ queryKey: ['templates'] })
    },
    onError: (err: Error) => setCloneError(err.message),
  })

  const handleDelete = (t: TemplateDto) => {
    if (!confirm(`Delete template "${t.name}"? This cannot be undone.`)) return
    deleteTemplate.mutate(t.id)
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-base font-semibold text-gray-900 dark:text-white">Release note templates</h1>
          <p className="text-sm text-gray-500 dark:text-gray-400 mt-0.5">
            Reusable Handlebars templates for generating release notes.
          </p>
        </div>
        <button
          onClick={() => setSlideOver('new')}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          New template
        </button>
      </div>

      {/* Table */}
      {isLoading ? (
        <p className="text-sm text-gray-400">Loading…</p>
      ) : templates.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 dark:border-gray-600 p-12 text-center">
          <p className="text-sm text-gray-500 dark:text-gray-400">No templates yet.</p>
          <button
            onClick={() => setSlideOver('new')}
            className="mt-4 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
          >
            Create first template
          </button>
        </div>
      ) : (
        <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
            <thead>
              <tr className="bg-gray-50 dark:bg-gray-900/40">
                {['Name', 'Created', ''].map((h) => (
                  <th
                    key={h}
                    className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-gray-700/50 bg-white dark:bg-gray-800">
              {templates.map((t) => (
                <tr key={t.id} className="hover:bg-gray-50 dark:hover:bg-gray-700/30 transition-colors">
                  <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">
                    <span className="flex items-center gap-2">
                      {t.name}
                      {t.isSystem && (
                        <span className="inline-flex items-center rounded px-1.5 py-0.5 text-xs font-medium bg-violet-100 text-violet-700 dark:bg-violet-900/40 dark:text-violet-300 ring-1 ring-inset ring-violet-300 dark:ring-violet-700">
                          System
                        </span>
                      )}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-500 dark:text-gray-400 text-xs">
                    {new Date(t.createdAt).toLocaleDateString()}
                  </td>
                  <td className="px-4 py-3 text-right">
                    <div className="flex justify-end gap-3">
                      <button
                        onClick={() => setPreviewing(t)}
                        className="text-xs text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 font-medium"
                      >
                        Preview
                      </button>
                      {t.isSystem ? (
                        <button
                          onClick={() => cloneTemplate.mutate(t.id)}
                          disabled={cloneTemplate.isPending}
                          className="text-xs text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-200 font-medium disabled:opacity-50"
                        >
                          {cloneTemplate.isPending ? 'Cloning…' : 'Clone'}
                        </button>
                      ) : (
                        <>
                          <button
                            onClick={() => setSlideOver(t)}
                            className="text-xs text-blue-600 hover:text-blue-800 dark:text-blue-400 dark:hover:text-blue-200 font-medium"
                          >
                            Edit
                          </button>
                          <button
                            onClick={() => handleDelete(t)}
                            disabled={deleteTemplate.isPending}
                            className="text-xs text-red-500 hover:text-red-700 disabled:opacity-50"
                          >
                            Delete
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {cloneError && (
        <p className="text-sm text-red-500">{cloneError}</p>
      )}

      {/* Slide-over */}
      {slideOver && (
        <TemplateSlideOver
          template={slideOver === 'new' ? undefined : slideOver}
          onClose={() => setSlideOver(null)}
        />
      )}

      {/* Preview panel */}
      {previewing && (
        <TemplatePreviewPanel
          template={previewing}
          onClose={() => setPreviewing(null)}
        />
      )}
    </div>
  )
}
