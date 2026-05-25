import { useState } from 'react'
import type { components } from '../../../lib/api'
import {
  useProjectCustomVariables,
  useUpsertCustomVariable,
  useDeleteCustomVariable,
} from './hooks/useProjectCustomVariables'

type ProjectCustomVariableDto = components['schemas']['ProjectCustomVariableDto']

const RESERVED_KEYS = new Set(['project', 'version', 'repos', 'tickets', 'reconciliation', 'date'])

function SkeletonRow() {
  return (
    <tr>
      {[1, 2, 3].map((i) => (
        <td key={i} className="py-2 pr-4">
          <div className="h-4 rounded bg-gray-200 dark:bg-gray-700 animate-pulse" style={{ width: `${40 + i * 20}%` }} />
        </td>
      ))}
    </tr>
  )
}

interface InlineEditRowProps {
  variable: ProjectCustomVariableDto
  onSave: (key: string, value: string) => Promise<void>
  onDelete: (key: string) => Promise<void>
  isSaving: boolean
}

function InlineEditRow({ variable, onSave, onDelete, isSaving }: InlineEditRowProps) {
  const [editing, setEditing] = useState(false)
  const [value, setValue] = useState(variable.value)

  const handleSave = async () => {
    await onSave(variable.key, value)
    setEditing(false)
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') handleSave()
    if (e.key === 'Escape') {
      setValue(variable.value)
      setEditing(false)
    }
  }

  return (
    <tr className="hover:bg-gray-50 dark:hover:bg-gray-700/30">
      <td className="py-2 pr-4 font-mono text-xs text-gray-700 dark:text-gray-300">
        custom.{variable.key}
      </td>
      <td className="py-2 pr-4 flex-1">
        {editing ? (
          <input
            autoFocus
            value={value}
            onChange={(e) => setValue(e.target.value)}
            onKeyDown={handleKeyDown}
            className="w-full rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-sm px-2 py-1 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        ) : (
          <span className="text-sm text-gray-600 dark:text-gray-300">{variable.value}</span>
        )}
      </td>
      <td className="py-2 text-right whitespace-nowrap">
        {editing ? (
          <>
            <button
              onClick={handleSave}
              disabled={isSaving}
              className="text-xs text-blue-600 hover:text-blue-800 dark:text-blue-400 disabled:opacity-50 mr-3"
            >
              Save
            </button>
            <button
              onClick={() => { setValue(variable.value); setEditing(false) }}
              className="text-xs text-gray-500 hover:text-gray-700 dark:text-gray-400"
            >
              Cancel
            </button>
          </>
        ) : (
          <>
            <button
              onClick={() => setEditing(true)}
              className="text-xs text-blue-600 hover:text-blue-800 dark:text-blue-400 mr-3"
            >
              Edit
            </button>
            <button
              onClick={() => onDelete(variable.key)}
              disabled={isSaving}
              className="text-xs text-red-500 hover:text-red-700 disabled:opacity-50"
            >
              Delete
            </button>
          </>
        )}
      </td>
    </tr>
  )
}

interface AddRowProps {
  onAdd: (key: string, value: string) => Promise<void>
  isSaving: boolean
  existingKeys: Set<string>
}

function AddRow({ onAdd, isSaving, existingKeys }: AddRowProps) {
  const [key, setKey] = useState('')
  const [value, setValue] = useState('')
  const [error, setError] = useState<string | null>(null)

  const validate = (): string | null => {
    if (!key.trim()) return 'Key is required'
    if (!/^[a-zA-Z][a-zA-Z0-9_]*$/.test(key)) return 'Key must start with a letter and contain only letters, numbers, underscores'
    if (RESERVED_KEYS.has(key.toLowerCase())) return `"${key}" is a reserved key`
    if (existingKeys.has(key)) return `Key "${key}" already exists`
    return null
  }

  const handleAdd = async () => {
    const err = validate()
    if (err) { setError(err); return }
    setError(null)
    await onAdd(key.trim(), value)
    setKey('')
    setValue('')
  }

  return (
    <>
      <tr className="border-t border-gray-200 dark:border-gray-700">
        <td className="pt-3 pr-4">
          <div className="flex items-center gap-1">
            <span className="text-xs text-gray-400 font-mono">custom.</span>
            <input
              value={key}
              onChange={(e) => { setKey(e.target.value); setError(null) }}
              placeholder="myKey"
              className="w-28 rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-xs font-mono px-2 py-1 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </td>
        <td className="pt-3 pr-4">
          <input
            value={value}
            onChange={(e) => setValue(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleAdd()}
            placeholder="Value"
            className="w-full rounded border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-sm px-2 py-1 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
          />
        </td>
        <td className="pt-3 text-right">
          <button
            onClick={handleAdd}
            disabled={isSaving}
            className="rounded bg-blue-600 px-3 py-1 text-xs font-medium text-white hover:bg-blue-700 disabled:opacity-50"
          >
            {isSaving ? 'Adding…' : 'Add'}
          </button>
        </td>
      </tr>
      {error && (
        <tr>
          <td colSpan={3} className="pb-2">
            <p className="text-xs text-red-600">{error}</p>
          </td>
        </tr>
      )}
    </>
  )
}

interface CustomVariablesSectionProps {
  projectId: string
  isAdmin: boolean
}

export function CustomVariablesSection({ projectId, isAdmin }: CustomVariablesSectionProps) {
  const { data: variables = [], isLoading } = useProjectCustomVariables(projectId)
  const upsert = useUpsertCustomVariable(projectId)
  const del = useDeleteCustomVariable(projectId)

  const [toast, setToast] = useState<{ kind: 'success' | 'error'; message: string } | null>(null)

  const showToast = (kind: 'success' | 'error', message: string) => {
    setToast({ kind, message })
    setTimeout(() => setToast(null), 3000)
  }

  const handleSave = async (key: string, value: string) => {
    try {
      await upsert.mutateAsync({ key, value })
      showToast('success', `Variable "${key}" saved.`)
    } catch (err) {
      showToast('error', err instanceof Error ? err.message : 'Save failed')
    }
  }

  const handleDelete = async (key: string) => {
    if (!confirm(`Delete custom variable "${key}"?`)) return
    try {
      await del.mutateAsync(key)
      showToast('success', `Variable "${key}" deleted.`)
    } catch (err) {
      showToast('error', err instanceof Error ? err.message : 'Delete failed')
    }
  }

  const existingKeys = new Set(variables.map((v) => v.key))
  const isMutating = upsert.isPending || del.isPending

  return (
    <div className="space-y-3">
      <div>
        <h4 className="text-sm font-medium text-gray-700 dark:text-gray-300">Custom variables</h4>
        <p className="mt-0.5 text-xs text-gray-500 dark:text-gray-400">
          Use <code className="font-mono bg-gray-100 dark:bg-gray-700 px-1 rounded">{'{{custom.key}}'}</code> in templates to inject project-specific values.
        </p>
      </div>

      {toast && (
        <div
          className={`rounded-md px-3 py-2 text-xs font-medium ${
            toast.kind === 'success'
              ? 'bg-green-50 text-green-800 dark:bg-green-900/30 dark:text-green-300'
              : 'bg-red-50 text-red-700 dark:bg-red-900/30 dark:text-red-300'
          }`}
        >
          {toast.message}
        </div>
      )}

      <table className="min-w-full text-sm">
        <thead>
          <tr>
            {['Token', 'Value', ''].map((h) => (
              <th
                key={h}
                className="pb-1.5 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400 pr-4"
              >
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100 dark:divide-gray-700/50">
          {isLoading ? (
            <>
              <SkeletonRow />
              <SkeletonRow />
            </>
          ) : (
            variables.map((v) => (
              <InlineEditRow
                key={v.key}
                variable={v}
                onSave={handleSave}
                onDelete={handleDelete}
                isSaving={isMutating}
              />
            ))
          )}
          {isAdmin && (
            <AddRow onAdd={handleSave} isSaving={isMutating} existingKeys={existingKeys} />
          )}
        </tbody>
      </table>
    </div>
  )
}
