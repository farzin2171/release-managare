import { useState, useEffect } from 'react'
import type { PreparedPageSlot } from './store/useWizardStore'
import { useWizardStore } from './store/useWizardStore'

interface PreparedPageTabProps {
  slot: PreparedPageSlot
}

export function PreparedPageTab({ slot }: PreparedPageTabProps) {
  const editPage = useWizardStore((s) => s.editPage)

  const currentTitle =
    slot.draftState.kind === 'server'
      ? slot.serverTitle
      : slot.draftState.kind === 'edited'
      ? slot.draftState.title
      : slot.draftState.draftTitle
  const currentBody =
    slot.draftState.kind === 'server'
      ? slot.serverBody
      : slot.draftState.kind === 'edited'
      ? slot.draftState.body
      : slot.draftState.draftBody

  const [title, setTitle] = useState(currentTitle)
  const [body, setBody] = useState(currentBody)

  useEffect(() => {
    setTitle(currentTitle)
    setBody(currentBody)
  }, [slot.bindingId, currentTitle, currentBody])

  const titleError =
    title.length === 0
      ? 'Title is required'
      : title.length > 255
      ? `Title too long (${title.length}/255)`
      : null

  const handleTitleChange = (v: string) => {
    setTitle(v)
    if (v.length > 0 && v.length <= 255) {
      editPage(slot.bindingId, v, body)
    }
  }

  const handleBodyChange = (v: string) => {
    setBody(v)
    if (title.length > 0 && title.length <= 255) {
      editPage(slot.bindingId, title, v)
    }
  }

  return (
    <div className="space-y-4">
      {/* Title */}
      <div>
        <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
          Page title
        </label>
        <input
          value={title}
          onChange={(e) => handleTitleChange(e.target.value)}
          className={`w-full rounded-md border px-3 py-2 text-sm bg-white dark:bg-gray-700 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500 ${
            titleError
              ? 'border-red-400 focus:ring-red-400'
              : 'border-gray-300 dark:border-gray-600'
          }`}
        />
        {titleError && <p className="mt-1 text-xs text-red-600">{titleError}</p>}
      </div>

      {/* Unknown tokens badge list */}
      {slot.unknownTokens.length > 0 && (
        <div className="flex flex-wrap gap-1.5 items-center">
          <span className="text-xs text-amber-700 dark:text-amber-400 font-medium">
            Unknown tokens:
          </span>
          {slot.unknownTokens.map((token) => (
            <span
              key={token}
              className="inline-flex items-center rounded px-2 py-0.5 text-xs font-mono font-medium bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300"
            >
              {`{{${token}}}`}
            </span>
          ))}
        </div>
      )}

      {/* Body editor */}
      <div>
        <label className="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
          Body (Markdown)
        </label>
        <textarea
          value={body}
          onChange={(e) => handleBodyChange(e.target.value)}
          rows={20}
          className="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-sm font-mono px-3 py-2 text-gray-900 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500 resize-y"
          spellCheck={false}
        />
      </div>

      {slot.draftState.kind === 'edited' && (
        <p className="text-xs text-blue-600 dark:text-blue-400">
          Edited — changes saved locally.
        </p>
      )}
    </div>
  )
}
