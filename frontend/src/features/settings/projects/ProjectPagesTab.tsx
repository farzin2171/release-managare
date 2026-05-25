import { useState } from 'react'
import type { components } from '../../../lib/api'
import {
  useProjectBindings,
  useCreateBinding,
  useUpdateBinding,
  useDeleteBinding,
} from './hooks/useProjectBindings'
import { BindingFormSheet } from './BindingFormSheet'

type ProjectTemplateBindingDto = components['schemas']['ProjectTemplateBindingDto']
type CreateBindingRequest = components['schemas']['CreateBindingRequest']
type TemplateDto = components['schemas']['TemplateDto']

const KIND_LABELS: Record<string, string> = {
  ReleaseNotes: 'Release Notes',
  Checklist: 'Checklist',
  Custom: 'Custom',
}

const KIND_COLORS: Record<string, string> = {
  ReleaseNotes: 'bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300',
  Checklist: 'bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300',
  Custom: 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300',
}

function KindBadge({ kind }: { kind: string }) {
  return (
    <span
      className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${KIND_COLORS[kind] ?? KIND_COLORS.Custom}`}
    >
      {KIND_LABELS[kind] ?? kind}
    </span>
  )
}

function SkeletonRow() {
  return (
    <tr>
      {[1, 2, 3, 4, 5, 6].map((i) => (
        <td key={i} className="py-3 pr-4">
          <div className="h-4 rounded bg-gray-200 dark:bg-gray-700 animate-pulse" style={{ width: `${50 + i * 10}%` }} />
        </td>
      ))}
    </tr>
  )
}

interface ProjectPagesTabProps {
  projectId: string
  templates: TemplateDto[]
  isAdmin: boolean
}

export function ProjectPagesTab({ projectId, templates, isAdmin }: ProjectPagesTabProps) {
  const { data: bindings = [], isLoading } = useProjectBindings(projectId)
  const createBinding = useCreateBinding(projectId)
  const updateBinding = useUpdateBinding(projectId)
  const deleteBinding = useDeleteBinding(projectId)

  const [sheetOpen, setSheetOpen] = useState(false)
  const [editingBinding, setEditingBinding] = useState<ProjectTemplateBindingDto | undefined>()
  const [saveError, setSaveError] = useState<string | null>(null)
  const [toast, setToast] = useState<{ kind: 'success' | 'error'; message: string } | null>(null)

  const showToast = (kind: 'success' | 'error', message: string) => {
    setToast({ kind, message })
    setTimeout(() => setToast(null), 3500)
  }

  const openAdd = () => {
    setEditingBinding(undefined)
    setSaveError(null)
    setSheetOpen(true)
  }

  const openEdit = (b: ProjectTemplateBindingDto) => {
    setEditingBinding(b)
    setSaveError(null)
    setSheetOpen(true)
  }

  const handleSave = async (data: CreateBindingRequest) => {
    setSaveError(null)
    try {
      if (editingBinding) {
        await updateBinding.mutateAsync({ bindingId: editingBinding.id, req: data })
        showToast('success', 'Binding updated.')
      } else {
        await createBinding.mutateAsync(data)
        showToast('success', 'Binding added.')
      }
      setSheetOpen(false)
    } catch (err) {
      const msg = err instanceof Error ? err.message : 'Save failed'
      setSaveError(msg)
    }
  }

  const handleDelete = async (b: ProjectTemplateBindingDto) => {
    if (!confirm(`Delete "${b.pageTitleTemplate}"?`)) return
    try {
      await deleteBinding.mutateAsync(b.id)
      showToast('success', 'Binding deleted.')
    } catch (err) {
      showToast('error', err instanceof Error ? err.message : 'Delete failed')
    }
  }

  const isSaving = createBinding.isPending || updateBinding.isPending

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <p className="text-sm text-gray-500 dark:text-gray-400">
          Bind page templates to this project. Each binding auto-fills a Confluence page in the release wizard.
        </p>
        {isAdmin && (
          <button
            onClick={openAdd}
            className="shrink-0 rounded-md bg-blue-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-blue-700"
          >
            + Add binding
          </button>
        )}
      </div>

      {toast && (
        <div
          className={`rounded-md px-4 py-2 text-sm font-medium ${
            toast.kind === 'success'
              ? 'bg-green-50 text-green-800 dark:bg-green-900/30 dark:text-green-300'
              : 'bg-red-50 text-red-700 dark:bg-red-900/30 dark:text-red-300'
          }`}
        >
          {toast.message}
        </div>
      )}

      {isLoading ? (
        <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
          <thead>
            <TableHead />
          </thead>
          <tbody className="divide-y divide-gray-100 dark:divide-gray-700/50">
            <SkeletonRow />
            <SkeletonRow />
          </tbody>
        </table>
      ) : bindings.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 dark:border-gray-600 px-6 py-8 text-center">
          <p className="text-sm text-gray-500 dark:text-gray-400">
            No page bindings yet.{' '}
            {isAdmin && (
              <button onClick={openAdd} className="text-blue-600 hover:underline dark:text-blue-400">
                Add one now.
              </button>
            )}
          </p>
        </div>
      ) : (
        <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
          <thead>
            <TableHead />
          </thead>
          <tbody className="divide-y divide-gray-100 dark:divide-gray-700/50">
            {bindings.map((b) => (
              <tr key={b.id} className="hover:bg-gray-50 dark:hover:bg-gray-700/30">
                <td className="py-2 pr-4">
                  <KindBadge kind={b.kind} />
                </td>
                <td className="py-2 pr-4 font-mono text-xs text-gray-700 dark:text-gray-300 max-w-xs truncate">
                  {b.pageTitleTemplate}
                </td>
                <td className="py-2 pr-4 text-xs text-gray-500 dark:text-gray-400">
                  {b.parentPageId ?? <span className="italic">Project default</span>}
                </td>
                <td className="py-2 pr-4 text-center">
                  {b.linkFromReleaseNotes ? (
                    <span className="text-green-600 dark:text-green-400" title="Linked from release notes">✓</span>
                  ) : (
                    <span className="text-gray-300 dark:text-gray-600">—</span>
                  )}
                </td>
                <td className="py-2 pr-4 text-gray-500 dark:text-gray-400 text-center">{b.sortOrder}</td>
                {isAdmin && (
                  <td className="py-2 text-right whitespace-nowrap">
                    <button
                      onClick={() => openEdit(b)}
                      className="text-xs text-blue-600 hover:text-blue-800 dark:text-blue-400 mr-3"
                    >
                      Edit
                    </button>
                    <button
                      onClick={() => handleDelete(b)}
                      disabled={deleteBinding.isPending}
                      className="text-xs text-red-500 hover:text-red-700 disabled:opacity-50"
                    >
                      Delete
                    </button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {sheetOpen && (
        <BindingFormSheet
          binding={editingBinding}
          templates={templates}
          onSave={handleSave}
          onClose={() => setSheetOpen(false)}
          isSaving={isSaving}
          saveError={saveError}
        />
      )}
    </div>
  )
}

function TableHead() {
  return (
    <tr>
      {['Kind', 'Title template', 'Parent page', 'Linked', 'Order', ''].map((h) => (
        <th
          key={h}
          className="pb-2 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 pr-4"
        >
          {h}
        </th>
      ))}
    </tr>
  )
}
