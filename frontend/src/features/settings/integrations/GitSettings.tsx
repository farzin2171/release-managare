import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'

type GitConnectionDto = components['schemas']['GitConnectionDto']

const schema = z.object({
  name: z.string().min(1, 'Name is required'),
  organizationUrl: z.string().url('Must be a valid URL'),
  pat: z.string(),
})
type FormData = z.infer<typeof schema>

type TestState = 'idle' | 'pending' | 'success' | 'error'

function StatusBadge({ status }: { status: GitConnectionDto['lastTestStatus'] }) {
  if (status === 'Success')
    return (
      <span className="inline-flex items-center rounded px-2 py-0.5 text-xs font-medium bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300">
        Connected
      </span>
    )
  if (status === 'Failed')
    return (
      <span className="inline-flex items-center rounded px-2 py-0.5 text-xs font-medium bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300">
        Failed
      </span>
    )
  return (
    <span className="inline-flex items-center rounded px-2 py-0.5 text-xs font-medium bg-gray-100 text-gray-600 dark:bg-gray-700 dark:text-gray-400">
      Not tested
    </span>
  )
}

export function GitSettings() {
  const qc = useQueryClient()
  const [testState, setTestState] = useState<TestState>('idle')
  const [testMessage, setTestMessage] = useState('')
  const [syncingId, setSyncingId] = useState<string | null>(null)
  const [editingId, setEditingId] = useState<string | null>(null)

  const { data: connections = [], isLoading } = useQuery<GitConnectionDto[]>({
    queryKey: ['git-connections'],
    queryFn: () => apiFetch('/api/v1/integrations/git').then((r) => r.json()),
  })

  const {
    register,
    handleSubmit,
    reset,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({ resolver: zodResolver(schema) })

  const save = useMutation({
    mutationFn: async (data: FormData) => {
      if (editingId) {
        const body: Record<string, unknown> = { name: data.name, organizationUrl: data.organizationUrl }
        if (data.pat) body.pat = data.pat
        const res = await apiFetch(`/api/v1/integrations/git/${editingId}`, {
          method: 'PUT',
          body: JSON.stringify(body),
        })
        if (!res.ok) throw new Error('Failed to update connection')
        return res.json()
      }
      if (!data.pat) throw new Error('PAT is required when creating a connection')
      const res = await apiFetch('/api/v1/integrations/git', {
        method: 'POST',
        body: JSON.stringify({ ...data, providerType: 'AzureDevOps' }),
      })
      if (!res.ok) throw new Error('Failed to create connection')
      return res.json()
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['git-connections'] })
      setEditingId(null)
      reset({ name: '', organizationUrl: '', pat: '' })
    },
  })

  const handleTest = async () => {
    const data = getValues()
    if (!data.organizationUrl || !data.pat) {
      setTestState('error')
      setTestMessage('Organisation URL and PAT are required to test the connection')
      return
    }
    setTestState('pending')
    setTestMessage('')
    try {
      const res = await apiFetch('/api/v1/integrations/git/test', {
        method: 'POST',
        body: JSON.stringify({ providerType: 'AzureDevOps', organizationUrl: data.organizationUrl, pat: data.pat }),
      })
      const body = await res.json()
      if (res.ok && body.success) {
        setTestState('success')
        setTestMessage(body.message ?? 'Connection successful')
      } else {
        setTestState('error')
        setTestMessage(body.detail ?? body.message ?? 'Connection failed')
      }
    } catch {
      setTestState('error')
      setTestMessage('Could not reach the server')
    }
  }

  const handleSync = async (id: string) => {
    setSyncingId(id)
    try {
      await apiFetch(`/api/v1/integrations/git/${id}/sync`, { method: 'POST' })
      qc.invalidateQueries({ queryKey: ['repositories'] })
      qc.invalidateQueries({ queryKey: ['git-connections'] })
    } finally {
      setSyncingId(null)
    }
  }

  const startEdit = (conn: GitConnectionDto) => {
    setEditingId(conn.id)
    reset({ name: conn.name, organizationUrl: conn.organizationUrl, pat: '' })
    setTestState('idle')
    setTestMessage('')
  }

  const cancelEdit = () => {
    setEditingId(null)
    reset({ name: '', organizationUrl: '', pat: '' })
    setTestState('idle')
    setTestMessage('')
  }

  const isAdding = editingId === null && connections.length === 0
  const showForm = editingId !== null || isAdding

  if (isLoading) return <p className="text-sm text-gray-500">Loading…</p>

  return (
    <div className="max-w-2xl space-y-6">
      <div>
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Azure DevOps</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Connect your Azure DevOps organisation to sync repositories.
        </p>
      </div>

      {connections.length > 0 && (
        <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
            <thead className="bg-gray-50 dark:bg-gray-800">
              <tr>
                {['Name', 'Organisation URL', 'Last sync', 'Status', ''].map((h) => (
                  <th
                    key={h}
                    className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="bg-white dark:bg-gray-900 divide-y divide-gray-200 dark:divide-gray-700">
              {connections.map((conn) => (
                <tr key={conn.id}>
                  <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">{conn.name}</td>
                  <td className="px-4 py-3 text-gray-500 dark:text-gray-400 truncate max-w-xs">
                    {conn.organizationUrl}
                  </td>
                  <td className="px-4 py-3 text-gray-500 dark:text-gray-400">
                    {conn.lastSyncedAt ? new Date(conn.lastSyncedAt).toLocaleString() : '—'}
                  </td>
                  <td className="px-4 py-3">
                    <StatusBadge status={conn.lastTestStatus} />
                  </td>
                  <td className="px-4 py-3 text-right space-x-3">
                    <button
                      onClick={() => handleSync(conn.id)}
                      disabled={syncingId === conn.id}
                      className="text-blue-600 hover:text-blue-800 dark:text-blue-400 disabled:opacity-50"
                    >
                      {syncingId === conn.id ? 'Syncing…' : 'Sync now'}
                    </button>
                    <button
                      onClick={() => startEdit(conn)}
                      className="text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
                    >
                      Edit
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {!showForm && connections.length > 0 && (
        <button
          onClick={() => {
            setEditingId('new')
            reset({ name: '', organizationUrl: '', pat: '' })
          }}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          Add connection
        </button>
      )}

      {showForm && (
        <div className="rounded-lg border border-gray-200 dark:border-gray-700 p-6">
          <h3 className="text-sm font-semibold text-gray-900 dark:text-white mb-4">
            {editingId && editingId !== 'new' ? 'Update connection' : 'Add connection'}
          </h3>
          <form onSubmit={handleSubmit((d) => save.mutate(d))} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Name
              </label>
              <input
                {...register('name')}
                placeholder="My AzDO Org"
                className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              {errors.name && <p className="mt-1 text-xs text-red-500">{errors.name.message}</p>}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Organisation URL
              </label>
              <input
                {...register('organizationUrl')}
                placeholder="https://dev.azure.com/myorg"
                className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              {errors.organizationUrl && (
                <p className="mt-1 text-xs text-red-500">{errors.organizationUrl.message}</p>
              )}
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
                Personal Access Token{editingId && editingId !== 'new' && ' (leave blank to keep existing)'}
              </label>
              <input
                {...register('pat')}
                type="password"
                autoComplete="off"
                className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
              />
              {errors.pat && <p className="mt-1 text-xs text-red-500">{errors.pat.message}</p>}
            </div>

            {testState !== 'idle' && (
              <div
                className={`rounded-md px-3 py-2 text-sm ${
                  testState === 'success'
                    ? 'bg-green-50 text-green-700 dark:bg-green-900/20 dark:text-green-400'
                    : testState === 'error'
                    ? 'bg-red-50 text-red-700 dark:bg-red-900/20 dark:text-red-400'
                    : 'bg-gray-50 text-gray-600 dark:bg-gray-800 dark:text-gray-400'
                }`}
              >
                {testState === 'pending' ? 'Testing connection…' : testMessage}
              </div>
            )}

            {save.isError && (
              <p className="text-sm text-red-500">
                {(save.error as Error).message}
              </p>
            )}

            <div className="flex gap-2">
              <button
                type="button"
                onClick={handleTest}
                disabled={testState === 'pending'}
                className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700 disabled:opacity-50"
              >
                Test connection
              </button>
              <button
                type="submit"
                disabled={isSubmitting || save.isPending}
                className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50"
              >
                {isSubmitting || save.isPending ? 'Saving…' : 'Save'}
              </button>
              {(editingId || connections.length > 0) && (
                <button
                  type="button"
                  onClick={cancelEdit}
                  className="rounded-md px-4 py-2 text-sm font-medium text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200"
                >
                  Cancel
                </button>
              )}
            </div>
          </form>
        </div>
      )}
    </div>
  )
}
