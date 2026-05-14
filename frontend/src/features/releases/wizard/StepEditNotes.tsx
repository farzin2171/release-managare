import { useState, useCallback } from 'react'
import { useMutation } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'

interface StepEditNotesProps {
  releaseId: string
  initialNotes: string
  onBack: () => void
  onNext: (savedNotes: string) => void
}

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
    .replace(/^\s*\d+\.\s+(.+)$/gm, '<li class="ml-4 list-decimal">$1</li>')
    .replace(/\[(.+?)\]\((.+?)\)/g, '<a href="$2" class="text-blue-600 hover:underline" target="_blank" rel="noreferrer">$1</a>')
    .replace(/\n{2,}/g, '</p><p class="mb-2">')
    .replace(/^(?!<[hua])(.+)$/gm, '$1')
}

export function StepEditNotes({ releaseId, initialNotes, onBack, onNext }: StepEditNotesProps) {
  const [notes, setNotes] = useState(initialNotes)
  const [saved, setSaved] = useState(true)

  const saveMutation = useMutation({
    mutationFn: (markdown: string) =>
      apiFetch(`/api/v1/releases/${releaseId}`, {
        method: 'PUT',
        body: JSON.stringify({ editedNotesMarkdown: markdown }),
      }).then((r) => {
        if (!r.ok) throw new Error('Failed to save notes')
        return r.json()
      }),
    onSuccess: (_, markdown) => {
      setSaved(true)
      onNext(markdown)
    },
  })

  const handleChange = useCallback((val: string) => {
    setNotes(val)
    setSaved(false)
  }, [])

  const handleNext = () => {
    saveMutation.mutate(notes)
  }

  const preview = renderMarkdownPreview(notes)

  return (
    <div className="space-y-4">
      <div>
        <h2 className="text-base font-semibold text-gray-900 dark:text-white">Step 3 — Edit release notes</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Edit the generated markdown on the left; see a live preview on the right.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 min-h-[400px]">
        {/* Editor */}
        <div className="flex flex-col">
          <p className="text-xs font-medium uppercase tracking-wider text-gray-400 mb-1.5">Markdown</p>
          <textarea
            value={notes}
            onChange={(e) => handleChange(e.target.value)}
            className="flex-1 min-h-[380px] rounded-lg border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-900 text-gray-900 dark:text-white font-mono text-sm p-3 resize-none focus:outline-none focus:ring-2 focus:ring-blue-500"
            placeholder="# Release notes&#10;&#10;## Features&#10;- ..."
            spellCheck={false}
          />
        </div>

        {/* Preview */}
        <div className="flex flex-col">
          <p className="text-xs font-medium uppercase tracking-wider text-gray-400 mb-1.5">Preview</p>
          <div
            className="flex-1 min-h-[380px] rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 p-4 overflow-auto prose prose-sm dark:prose-invert max-w-none text-sm text-gray-800 dark:text-gray-200"
            dangerouslySetInnerHTML={{ __html: `<p class="mb-2">${preview}</p>` }}
          />
        </div>
      </div>

      {saveMutation.isError && (
        <p className="text-sm text-red-500">Failed to save notes. Please try again.</p>
      )}

      <div className="flex items-center justify-between">
        <button
          onClick={onBack}
          className="px-5 py-2 rounded-md border border-gray-300 dark:border-gray-600 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
        >
          Back
        </button>
        <div className="flex items-center gap-3">
          {!saved && <span className="text-xs text-gray-400">Unsaved changes</span>}
          <button
            onClick={handleNext}
            disabled={saveMutation.isPending}
            className="px-5 py-2 rounded-md bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
          >
            {saveMutation.isPending ? 'Saving…' : 'Save &amp; continue'}
          </button>
        </div>
      </div>
    </div>
  )
}
