import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'

type UserDto = components['schemas']['UserDto']

const newUserSchema = z.object({
  email: z.string().email('Must be a valid email'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
  role: z.enum(['Admin', 'Viewer']),
})
type NewUserFormData = z.infer<typeof newUserSchema>

function RoleChip({ role }: { role: string }) {
  const cls =
    role === 'Admin'
      ? 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300'
      : 'bg-gray-100 text-gray-700 dark:bg-gray-700 dark:text-gray-300'
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ${cls}`}>
      {role}
    </span>
  )
}

function NewUserModal({ onClose }: { onClose: () => void }) {
  const qc = useQueryClient()
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<NewUserFormData>({
    resolver: zodResolver(newUserSchema),
    defaultValues: { role: 'Viewer' },
  })

  const create = useMutation({
    mutationFn: async (data: NewUserFormData) => {
      const res = await apiFetch('/api/v1/users', {
        method: 'POST',
        body: JSON.stringify(data),
      })
      if (!res.ok) {
        const err = await res.json().catch(() => ({}))
        throw new Error((err as { title?: string }).title ?? 'Failed to create user')
      }
      return res.json() as Promise<UserDto>
    },
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['users'] })
      onClose()
    },
  })

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-md bg-white dark:bg-gray-800 rounded-xl shadow-lg p-6 space-y-4">
        <h2 className="text-base font-semibold text-gray-900 dark:text-white">New user</h2>
        <form onSubmit={handleSubmit((d) => create.mutate(d))} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Email
            </label>
            <input
              {...register('email')}
              type="email"
              autoFocus
              className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            {errors.email && <p className="mt-1 text-xs text-red-500">{errors.email.message}</p>}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Password
            </label>
            <input
              {...register('password')}
              type="password"
              className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
            {errors.password && (
              <p className="mt-1 text-xs text-red-500">{errors.password.message}</p>
            )}
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
              Role
            </label>
            <select
              {...register('role')}
              className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
            >
              <option value="Viewer">Viewer</option>
              <option value="Admin">Admin</option>
            </select>
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
              {isSubmitting || create.isPending ? 'Creating…' : 'Create user'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export function UsersPage() {
  const qc = useQueryClient()
  const [showCreate, setShowCreate] = useState(false)
  const [deactivatingId, setDeactivatingId] = useState<string | null>(null)

  const { data: users = [], isLoading } = useQuery<UserDto[]>({
    queryKey: ['users'],
    queryFn: () => apiFetch('/api/v1/users').then((r) => r.json()),
  })

  const deactivate = useMutation({
    mutationFn: async (id: string) => {
      const res = await apiFetch(`/api/v1/users/${id}`, {
        method: 'PUT',
        body: JSON.stringify({ isActive: false }),
      })
      if (!res.ok) throw new Error('Failed to deactivate user')
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
    onSettled: () => setDeactivatingId(null),
  })

  const activate = useMutation({
    mutationFn: async (id: string) => {
      const res = await apiFetch(`/api/v1/users/${id}`, {
        method: 'PUT',
        body: JSON.stringify({ isActive: true }),
      })
      if (!res.ok) throw new Error('Failed to activate user')
    },
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
  })

  const handleDeactivate = (user: UserDto) => {
    if (!confirm(`Deactivate ${user.email}? They will no longer be able to log in.`)) return
    setDeactivatingId(user.id)
    deactivate.mutate(user.id)
  }

  return (
    <div className="max-w-4xl space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-lg font-semibold text-gray-900 dark:text-white">Users</h2>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Manage user accounts and roles. Viewer accounts can read all data but cannot make changes.
          </p>
        </div>
        <button
          onClick={() => setShowCreate(true)}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
        >
          New user
        </button>
      </div>

      {isLoading ? (
        <p className="text-sm text-gray-500">Loading…</p>
      ) : users.length === 0 ? (
        <p className="text-sm text-gray-400">No users found.</p>
      ) : (
        <div className="rounded-lg border border-gray-200 dark:border-gray-700 overflow-hidden">
          <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
            <thead className="bg-gray-50 dark:bg-gray-800">
              <tr>
                {['Email', 'Role', 'Status', 'Last login', ''].map((h) => (
                  <th
                    key={h}
                    className="px-4 py-3 text-left text-xs font-medium uppercase tracking-wider text-gray-500 dark:text-gray-400"
                  >
                    {h}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody className="bg-white dark:bg-gray-900 divide-y divide-gray-100 dark:divide-gray-800">
              {users.map((u) => (
                <tr key={u.id} className={!u.isActive ? 'opacity-50' : ''}>
                  <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">
                    {u.email}
                  </td>
                  <td className="px-4 py-3">
                    <RoleChip role={u.role} />
                  </td>
                  <td className="px-4 py-3">
                    {u.isActive ? (
                      <span className="inline-flex items-center gap-1 text-xs text-green-700 dark:text-green-400">
                        <span className="w-1.5 h-1.5 rounded-full bg-green-500 inline-block" />
                        Active
                      </span>
                    ) : (
                      <span className="inline-flex items-center gap-1 text-xs text-gray-400">
                        <span className="w-1.5 h-1.5 rounded-full bg-gray-400 inline-block" />
                        Inactive
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-3 text-gray-500 dark:text-gray-400 text-xs">
                    {u.lastLoginAt
                      ? new Date(u.lastLoginAt).toLocaleString()
                      : '—'}
                  </td>
                  <td className="px-4 py-3 text-right">
                    {u.isActive ? (
                      <button
                        onClick={() => handleDeactivate(u)}
                        disabled={deactivatingId === u.id}
                        className="text-xs text-red-500 hover:text-red-700 disabled:opacity-50"
                      >
                        {deactivatingId === u.id ? 'Deactivating…' : 'Deactivate'}
                      </button>
                    ) : (
                      <button
                        onClick={() => activate.mutate(u.id)}
                        disabled={activate.isPending}
                        className="text-xs text-blue-600 hover:text-blue-800 disabled:opacity-50 dark:text-blue-400"
                      >
                        Activate
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showCreate && <NewUserModal onClose={() => setShowCreate(false)} />}
    </div>
  )
}
