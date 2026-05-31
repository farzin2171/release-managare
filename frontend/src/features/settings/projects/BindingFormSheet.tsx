import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import type { components } from '../../../lib/api'

type ProjectTemplateBindingDto = components['schemas']['ProjectTemplateBindingDto']
type CreateBindingRequest = components['schemas']['CreateBindingRequest']
type TemplateDto = components['schemas']['TemplateDto']

const bindingSchema = z.object({
  templateId: z.string().uuid('Must be a valid template'),
  kind: z.enum(['ReleaseNotes', 'Checklist', 'Custom', 'ReleaseSummary']),
  pageTitleTemplate: z.string().min(1).max(500),
  parentPageId: z.string().max(100).optional().nullable(),
  linkFromReleaseNotes: z.boolean(),
  sortOrder: z.number().int().min(0),
})
type BindingFormData = z.infer<typeof bindingSchema>

interface BindingFormSheetProps {
  binding?: ProjectTemplateBindingDto
  templates: TemplateDto[]
  onSave: (data: CreateBindingRequest) => Promise<void>
  onClose: () => void
  isSaving: boolean
  saveError: string | null
}

export function BindingFormSheet({
  binding,
  templates,
  onSave,
  onClose,
  isSaving,
  saveError,
}: BindingFormSheetProps) {
  const isEdit = !!binding
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<BindingFormData>({
    resolver: zodResolver(bindingSchema),
    defaultValues: {
      templateId: binding?.templateId ?? '',
      kind: (binding?.kind as BindingFormData['kind']) ?? 'ReleaseNotes',
      pageTitleTemplate: binding?.pageTitleTemplate ?? '{{project.name}} {{version}} — Release Notes',
      parentPageId: binding?.parentPageId ?? null,
      linkFromReleaseNotes: binding?.linkFromReleaseNotes ?? false,
      sortOrder: binding?.sortOrder ?? 0,
    },
  })

  useEffect(() => {
    if (binding) {
      reset({
        templateId: binding.templateId,
        kind: binding.kind as BindingFormData['kind'],
        pageTitleTemplate: binding.pageTitleTemplate,
        parentPageId: binding.parentPageId ?? null,
        linkFromReleaseNotes: binding.linkFromReleaseNotes,
        sortOrder: binding.sortOrder,
      })
    }
  }, [binding, reset])

  const onSubmit = async (data: BindingFormData) => {
    await onSave({
      templateId: data.templateId,
      kind: data.kind,
      pageTitleTemplate: data.pageTitleTemplate,
      parentPageId: data.parentPageId ?? null,
      linkFromReleaseNotes: data.linkFromReleaseNotes,
      sortOrder: data.sortOrder,
    })
  }

  return (
    <div className="fixed inset-0 z-50 flex">
      <div className="flex-1 bg-black/40" onClick={onClose} />
      <div className="w-full max-w-lg bg-white dark:bg-gray-800 shadow-xl flex flex-col overflow-y-auto">
        <div className="flex items-center justify-between px-6 py-4 border-b border-gray-200 dark:border-gray-700">
          <h2 className="text-base font-semibold text-gray-900 dark:text-white">
            {isEdit ? 'Edit page binding' : 'Add page binding'}
          </h2>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 text-xl"
            aria-label="Close"
          >
            ×
          </button>
        </div>

        <form onSubmit={handleSubmit(onSubmit)} className="flex flex-col flex-1 px-6 py-6 gap-5">
          {saveError && (
            <div className="text-sm text-red-600 bg-red-50 rounded p-3">{saveError}</div>
          )}

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Template
            </label>
            <select
              {...register('templateId')}
              className="w-full rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-sm px-3 py-2 text-gray-900 dark:text-white"
            >
              <option value="">Select a template…</option>
              {templates.map((t) => (
                <option key={t.id} value={t.id}>
                  {t.name}
                </option>
              ))}
            </select>
            {errors.templateId && (
              <p className="mt-1 text-xs text-red-600">{errors.templateId.message}</p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Kind
            </label>
            <select
              {...register('kind')}
              className="w-full rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-sm px-3 py-2 text-gray-900 dark:text-white"
            >
              <option value="ReleaseNotes">Release Notes</option>
              <option value="Checklist">Checklist</option>
              <option value="Custom">Custom</option>
              <option value="ReleaseSummary">Release Summary</option>
            </select>
            {errors.kind && (
              <p className="mt-1 text-xs text-red-600">{errors.kind.message}</p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Page title template
            </label>
            <input
              {...register('pageTitleTemplate')}
              className="w-full rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-sm px-3 py-2 text-gray-900 dark:text-white"
              placeholder="e.g. {{project.name}} {{version}} — Release Notes"
            />
            {errors.pageTitleTemplate && (
              <p className="mt-1 text-xs text-red-600">{errors.pageTitleTemplate.message}</p>
            )}
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Parent page ID (optional)
            </label>
            <input
              {...register('parentPageId')}
              className="w-full rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-sm px-3 py-2 text-gray-900 dark:text-white"
              placeholder="Confluence page ID (blank = project default)"
            />
          </div>

          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              {...register('linkFromReleaseNotes')}
              id="linkFromReleaseNotes"
              className="rounded border-gray-300"
            />
            <label
              htmlFor="linkFromReleaseNotes"
              className="text-sm text-gray-700 dark:text-gray-300"
            >
              Link from release notes page
            </label>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Sort order
            </label>
            <input
              type="number"
              {...register('sortOrder', { valueAsNumber: true })}
              className="w-24 rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-sm px-3 py-2 text-gray-900 dark:text-white"
              min={0}
            />
            {errors.sortOrder && (
              <p className="mt-1 text-xs text-red-600">{errors.sortOrder.message}</p>
            )}
          </div>

          <div className="flex gap-3 pt-2 mt-auto border-t border-gray-200 dark:border-gray-700">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700"
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isSaving}
              className="flex-1 rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
            >
              {isSaving ? 'Saving…' : isEdit ? 'Update' : 'Add binding'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
