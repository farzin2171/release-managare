import { useState } from 'react'
import { useParams, Link, useNavigate } from 'react-router-dom'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { apiFetch } from '../../lib/apiClient'
import type { components } from '../../lib/api'

type ReleaseDetailDto = components['schemas']['ReleaseDetailDto']

function renderMarkdownPreview(md: string): string {
  return md
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/^#{4}\s+(.+)$/gm, '<h4 class="text-sm font-semibold mt-4 mb-1">$1</h4>')
    .replace(/^#{3}\s+(.+)$/gm, '<h3 class="text-base font-semibold mt-5 mb-1">$1</h3>')
    .replace(/^#{2}\s+(.+)$/gm, '<h2 class="text-lg font-bold mt-6 mb-2">$1</h2>')
    .replace(/^#{1}\s+(.+)$/gm, '<h1 class="text-xl font-bold mt-6 mb-2">$1</h1>')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\*(.+?)\*/g, '<em>$1</em>')
    .replace(/`([^`\n]+)`/g, '<code class="px-1 py-0.5 rounded bg-gray-100 dark:bg-gray-800 font-mono text-xs">$1</code>')
    .replace(/```[\w]*\n([\s\S]*?)```/g, '<pre class="rounded bg-gray-100 dark:bg-gray-800 p-3 overflow-x-auto my-2"><code class="font-mono text-xs">$1</code></pre>')
    .replace(/^\s*[-*]\s+(.+)$/gm, '<li class="ml-4 list-disc">$1</li>')
    .replace(/(<li[\s\S]*?<\/li>\n?)+/g, (m) => `<ul class="my-2 space-y-0.5">${m}</ul>`)
    .replace(/\[(.+?)\]\((.+?)\)/g, '<a href="$2" class="text-blue-600 hover:underline" target="_blank" rel="noreferrer">$1</a>')
    .replace(/\n{2,}/g, '</p><p class="mb-2">')
}

function StatusBadge({ status }: { status: 'Draft' | 'Published' }) {
  return status === 'Published' ? (
    <span className="inline-flex items-center gap-1 rounded-full bg-green-100 dark:bg-green-900/30 px-2.5 py-0.5 text-xs font-medium text-green-800 dark:text-green-400">
      <span className="w-1.5 h-1.5 rounded-full bg-green-500 inline-block" />
      Published
    </span>
  ) : (
    <span className="inline-flex items-center gap-1 rounded-full bg-yellow-100 dark:bg-yellow-900/30 px-2.5 py-0.5 text-xs font-medium text-yellow-800 dark:text-yellow-400">
      <span className="w-1.5 h-1.5 rounded-full bg-yellow-500 inline-block" />
      Draft
    </span>
  )
}

export function ReleaseDetail() {
  const { id } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const qc = useQueryClient()

  const [editing, setEditing] = useState(false)
  const [editedNotes, setEditedNotes] = useState('')

  const { data: release, isLoading, isError } = useQuery<ReleaseDetailDto>({
    queryKey: ['release', id],
    queryFn: () => apiFetch(`/api/v1/releases/${id}`).then((r) => r.json()),
    enabled: !!id,
  })

  const saveNotesMutation = useMutation({
    mutationFn: (markdown: string) =>
      apiFetch(`/api/v1/releases/${id}`, {
        method: 'PUT',
        body: JSON.stringify({ editedNotesMarkdown: markdown }),
      }).then((r) => {
        if (!r.ok) throw new Error('Save failed')
        return r.json() as Promise<ReleaseDetailDto>
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['release', id] })
      setEditing(false)
    },
  })

  if (isLoading) {
    return <div className="p-8"><p className="text-sm text-gray-500">Loading release…</p></div>
  }

  if (isError || !release) {
    return <div className="p-8"><p className="text-sm text-red-500">Failed to load release.</p></div>
  }

  const isPublished = release.status === 'Published'
  const activeNotes = release.editedNotesMarkdown ?? release.generatedNotesMarkdown

  const startEditing = () => {
    setEditedNotes(activeNotes)
    setEditing(true)
  }

  return (
    <div className="max-w-5xl space-y-6 p-6">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400">
        <Link to="/projects" className="hover:text-gray-700 dark:hover:text-gray-200">Projects</Link>
        <span>/</span>
        <Link to={`/projects/${release.projectId}`} className="hover:text-gray-700 dark:hover:text-gray-200">
          Project
        </Link>
        <span>/</span>
        <span className="text-gray-900 dark:text-white font-medium">Release {release.version}</span>
      </nav>

      {/* Header */}
      <div className="flex items-start justify-between gap-4 flex-wrap">
        <div className="space-y-1">
          <div className="flex items-center gap-3">
            <h1 className="text-xl font-semibold text-gray-900 dark:text-white">Release {release.version}</h1>
            <StatusBadge status={release.status} />
          </div>
          <p className="text-sm text-gray-500 dark:text-gray-400">
            Created {new Date(release.createdAt).toLocaleDateString()}
            {isPublished && release.publishedAt &&
              ` · Published ${new Date(release.publishedAt).toLocaleDateString()}`}
          </p>
        </div>

        {/* Actions — hidden when published */}
        {!isPublished && !editing && (
          <div className="flex gap-2 shrink-0">
            <button
              onClick={startEditing}
              className="px-4 py-2 rounded-md border border-gray-300 dark:border-gray-600 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
            >
              Edit notes
            </button>
            <button
              onClick={() => navigate(`/releases/${id}/publish`)}
              className="px-4 py-2 rounded-md bg-green-600 text-white text-sm font-medium hover:bg-green-700 transition-colors"
            >
              Publish
            </button>
          </div>
        )}
      </div>

      {/* Confluence URL (published only) */}
      {isPublished && release.confluencePageUrl && (
        <div className="flex items-center gap-3 rounded-lg border border-green-200 dark:border-green-800 bg-green-50 dark:bg-green-900/20 px-4 py-3">
          <span className="text-green-600 dark:text-green-400 text-sm font-medium shrink-0">Confluence page</span>
          <a
            href={release.confluencePageUrl}
            target="_blank"
            rel="noreferrer"
            className="text-sm text-blue-600 hover:underline dark:text-blue-400 break-all"
          >
            {release.confluencePageUrl} ↗
          </a>
        </div>
      )}

      {/* Read-only notice for published releases */}
      {isPublished && (
        <div className="flex items-center gap-2 rounded-lg border border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800 px-4 py-3">
          <span className="text-gray-400 text-sm">🔒</span>
          <p className="text-sm text-gray-500 dark:text-gray-400">
            This release is published and locked. Release notes cannot be edited.
          </p>
        </div>
      )}

      {/* Repository tags table */}
      {release.repositoryTags.length > 0 && (
        <div>
          <h2 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Change ranges</h2>
          <div className="overflow-x-auto rounded-lg border border-gray-200 dark:border-gray-700">
            <table className="min-w-full divide-y divide-gray-200 dark:divide-gray-700 text-sm">
              <thead className="bg-gray-50 dark:bg-gray-800">
                <tr>
                  <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">Repository</th>
                  <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">From</th>
                  <th className="px-4 py-2.5 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">To</th>
                  <th className="px-4 py-2.5 text-right text-xs font-medium text-gray-500 uppercase tracking-wider">Commits</th>
                </tr>
              </thead>
              <tbody className="bg-white dark:bg-gray-900 divide-y divide-gray-100 dark:divide-gray-800">
                {release.repositoryTags.map((rt) => (
                  <tr key={rt.repositoryId}>
                    <td className="px-4 py-3 font-medium text-gray-900 dark:text-white">{rt.repositoryName}</td>
                    <td className="px-4 py-3 font-mono text-xs text-gray-500 dark:text-gray-400">{rt.fromTag ?? 'beginning'}</td>
                    <td className="px-4 py-3 font-mono text-xs text-gray-500 dark:text-gray-400">{rt.toTag}</td>
                    <td className="px-4 py-3 text-right tabular-nums text-gray-700 dark:text-gray-300">{rt.commitCount}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Notes section */}
      <div>
        <h2 className="text-sm font-medium text-gray-700 dark:text-gray-300 mb-2">Release notes</h2>

        {editing ? (
          <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
            <div className="flex flex-col">
              <p className="text-xs text-gray-400 mb-1.5">Markdown</p>
              <textarea
                value={editedNotes}
                onChange={(e) => setEditedNotes(e.target.value)}
                className="flex-1 min-h-[380px] rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-900 text-gray-900 dark:text-white font-mono text-sm p-3 resize-none focus:outline-none focus:ring-2 focus:ring-blue-500"
                spellCheck={false}
              />
              <div className="flex gap-2 mt-3 justify-end">
                <button
                  onClick={() => setEditing(false)}
                  className="px-4 py-2 rounded-md border border-gray-300 dark:border-gray-600 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
                >
                  Cancel
                </button>
                <button
                  onClick={() => saveNotesMutation.mutate(editedNotes)}
                  disabled={saveNotesMutation.isPending}
                  className="px-4 py-2 rounded-md bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-40 transition-colors"
                >
                  {saveNotesMutation.isPending ? 'Saving…' : 'Save'}
                </button>
              </div>
              {saveNotesMutation.isError && (
                <p className="mt-1 text-xs text-red-500">Save failed. Please try again.</p>
              )}
            </div>
            <div className="flex flex-col">
              <p className="text-xs text-gray-400 mb-1.5">Preview</p>
              <div
                className="flex-1 min-h-[380px] rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4 overflow-auto text-sm text-gray-800 dark:text-gray-200"
                dangerouslySetInnerHTML={{ __html: `<p class="mb-2">${renderMarkdownPreview(editedNotes)}</p>` }}
              />
            </div>
          </div>
        ) : (
          <div
            className="rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-5 text-sm text-gray-800 dark:text-gray-200"
            dangerouslySetInnerHTML={{ __html: `<p class="mb-2">${renderMarkdownPreview(activeNotes)}</p>` }}
          />
        )}
      </div>
    </div>
  )
}
