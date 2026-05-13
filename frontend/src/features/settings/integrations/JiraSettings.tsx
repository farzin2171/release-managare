import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'

type JiraConnectionDto = components['schemas']['JiraConnectionResponseDto']

const schema = z.object({
  baseUrl: z.string().url('Must be a valid URL (e.g. https://myorg.atlassian.net)'),
  email: z.string().email('Must be a valid email'),
  apiToken: z.string().min(1, 'API token is required'),
})
type FormData = z.infer<typeof schema>

type TestState = 'idle' | 'pending' | 'success' | 'error'

function StatusBadge({ status }: { status: JiraConnectionDto['lastTestStatus'] }) {
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

export function JiraSettings() {
  const qc = useQueryClient()
  const [testState, setTestState] = useState<TestState>('idle')
  const [testMessage, setTestMessage] = useState('')

  const { data: connection, isLoading } = useQuery<JiraConnectionDto | null>({
    queryKey: ['jira-connection'],
    queryFn: () =>
      apiFetch('/api/v1/integrations/jira').then((r) => (r.status === 404 ? null : r.json())),
  })

  const {
    register,
    handleSubmit,
    getValues,
    formState: { errors, isSubmitting },
  } = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: {
      baseUrl: connection?.baseUrl ?? '',
      email: connection?.email ?? '',
      apiToken: '',
    },
  })

  const save = useMutation({
    mutationFn: async (data: FormData) => {
      const res = await apiFetch('/api/v1/integrations/jira', {
        method: 'PUT',
        body: JSON.stringify(data),
      })
      if (!res.ok) throw new Error('Failed to save Jira connection')
      return res.json()
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['jira-connection'] }),
  })

  const handleTest = async () => {
    const data = getValues()
    setTestState('pending')
    setTestMessage('')
    try {
      const res = await apiFetch('/api/v1/integrations/jira/test', {
        method: 'POST',
        body: JSON.stringify(data),
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

  if (isLoading) return <p className="text-sm text-gray-500">Loading…</p>

  return (
    <div className="max-w-2xl space-y-6">
      <div>
        <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Jira</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Connect to Jira Cloud to reconcile tickets against releases.
        </p>
      </div>

      {connection && (
        <div className="flex items-center gap-3 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 px-4 py-3">
          <div className="flex-1 min-w-0">
            <p className="text-sm font-medium text-gray-900 dark:text-white truncate">
              {connection.baseUrl}
            </p>
            <p className="text-xs text-gray-500 dark:text-gray-400">{connection.email}</p>
          </div>
          <StatusBadge status={connection.lastTestStatus} />
        </div>
      )}

      <div className="rounded-lg border border-gray-200 dark:border-gray-700 p-6">
        <h3 className="text-sm font-semibold text-gray-900 dark:text-white mb-4">
          {connection ? 'Update connection' : 'Add connection'}
        </h3>
        <form onSubmit={handleSubmit((d) => save.mutate(d))} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Base URL
            </label>
            <input
              {...register('baseUrl')}
              defaultValue={connection?.baseUrl}
              placeholder="https://myorg.atlassian.net"
              className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            {errors.baseUrl && (
              <p className="mt-1 text-xs text-red-500">{errors.baseUrl.message}</p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Email
            </label>
            <input
              {...register('email')}
              type="email"
              defaultValue={connection?.email}
              autoComplete="username"
              className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            {errors.email && (
              <p className="mt-1 text-xs text-red-500">{errors.email.message}</p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              API Token{connection && ' (leave blank to keep existing)'}
            </label>
            <input
              {...register('apiToken')}
              type="password"
              autoComplete="off"
              className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            {errors.apiToken && (
              <p className="mt-1 text-xs text-red-500">{errors.apiToken.message}</p>
            )}
            <p className="mt-1 text-xs text-gray-400">
              Generate an API token at{' '}
              <span className="font-mono">id.atlassian.com → Security → API tokens</span>
            </p>
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
            <p className="text-sm text-red-500">{(save.error as Error).message}</p>
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
          </div>
        </form>
      </div>
    </div>
  )
}
