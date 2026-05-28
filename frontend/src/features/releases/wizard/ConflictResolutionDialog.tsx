import { useWizardStore, type PreparedPageSlot } from './store/useWizardStore'

interface ConflictResolutionDialogProps {
  onClose: () => void
}

function ConflictSlot({
  slot,
  onResolve,
}: {
  slot: PreparedPageSlot
  onResolve: (bindingId: string, resolution: 'keep' | 'discard') => void
}) {
  if (slot.draftState.kind !== 'conflict') return null
  const { serverTitle, draftTitle } = slot.draftState

  return (
    <div className="rounded-lg border border-amber-200 dark:border-amber-800 bg-amber-50 dark:bg-amber-900/20 p-4 space-y-3">
      <p className="text-sm font-medium text-gray-900 dark:text-white truncate" title={serverTitle}>
        {serverTitle}
      </p>

      <div className="grid grid-cols-2 gap-3 text-xs">
        <div className="rounded border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-800 p-2">
          <p className="font-semibold text-gray-500 dark:text-gray-400 mb-1">Fresh render</p>
          <p className="text-gray-700 dark:text-gray-300 truncate">{serverTitle}</p>
        </div>
        <div className="rounded border border-blue-200 dark:border-blue-800 bg-blue-50 dark:bg-blue-900/20 p-2">
          <p className="font-semibold text-blue-600 dark:text-blue-400 mb-1">Your edits</p>
          <p className="text-gray-700 dark:text-gray-300 truncate">{draftTitle}</p>
        </div>
      </div>

      <div className="flex gap-2">
        <button
          onClick={() => onResolve(slot.bindingId, 'keep')}
          className="flex-1 rounded-md border border-blue-600 px-3 py-1.5 text-xs font-medium text-blue-600 hover:bg-blue-50 dark:hover:bg-blue-900/30"
        >
          Keep my edits
        </button>
        <button
          onClick={() => onResolve(slot.bindingId, 'discard')}
          className="flex-1 rounded-md border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-xs font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700"
        >
          Use fresh render
        </button>
      </div>
    </div>
  )
}

export function ConflictResolutionDialog({ onClose }: ConflictResolutionDialogProps) {
  const { pages, resolveConflict } = useWizardStore()
  const conflictSlots = pages.filter((p) => p.draftState.kind === 'conflict')

  const handleResolve = (bindingId: string, resolution: 'keep' | 'discard') => {
    resolveConflict(bindingId, resolution)
  }

  const allResolved = conflictSlots.every((p) => p.draftState.kind !== 'conflict')

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="w-full max-w-lg rounded-xl bg-white dark:bg-gray-900 shadow-2xl p-6 space-y-4 mx-4">
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 className="text-base font-semibold text-gray-900 dark:text-white">
              Page conflicts detected
            </h2>
            <p className="text-sm text-gray-500 dark:text-gray-400 mt-0.5">
              The pages were re-rendered with new data. Choose what to keep for each conflicting page.
            </p>
          </div>
        </div>

        <div className="space-y-3 max-h-80 overflow-y-auto">
          {conflictSlots.map((slot) => (
            <ConflictSlot key={slot.bindingId} slot={slot} onResolve={handleResolve} />
          ))}
          {conflictSlots.length === 0 && (
            <p className="text-sm text-gray-500 dark:text-gray-400 text-center py-4">
              All conflicts resolved.
            </p>
          )}
        </div>

        <div className="flex justify-end pt-2 border-t border-gray-200 dark:border-gray-700">
          <button
            onClick={onClose}
            disabled={!allResolved && conflictSlots.length > 0}
            className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Done
          </button>
        </div>
      </div>
    </div>
  )
}
