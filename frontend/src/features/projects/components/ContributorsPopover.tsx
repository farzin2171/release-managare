import { useState, useRef, useEffect } from 'react'
import type { components } from '../../../lib/api'

type ContributorSnapshotDto = components['schemas']['ContributorSnapshotDto']

interface Props {
  contributors: ContributorSnapshotDto[]
  count: number
}

export function ContributorsPopover({ contributors, count }: Props) {
  const [open, setOpen] = useState(false)
  const ref = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!open) return
    function onMouseDown(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) {
        setOpen(false)
      }
    }
    document.addEventListener('mousedown', onMouseDown)
    return () => document.removeEventListener('mousedown', onMouseDown)
  }, [open])

  if (contributors.length === 0) {
    return (
      <span className="text-xl font-bold tabular-nums text-gray-900 dark:text-white">
        {count}
      </span>
    )
  }

  return (
    <div ref={ref} className="relative inline-block">
      <button
        type="button"
        onClick={(e) => { e.preventDefault(); e.stopPropagation(); setOpen((v) => !v) }}
        className="text-xl font-bold tabular-nums text-gray-900 dark:text-white cursor-pointer hover:underline focus:outline-none"
      >
        {count}
      </button>

      {open && (
        <div className="absolute z-20 bottom-full left-1/2 -translate-x-1/2 mb-2 w-52 rounded-lg border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 shadow-lg p-3">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-gray-500 dark:text-gray-400 mb-2">
            Contributors
          </p>
          <ul className="space-y-1 max-h-48 overflow-y-auto">
            {contributors.map((c) => (
              <li key={c.email || c.name} className="flex items-center justify-between gap-2">
                <span className="text-xs text-gray-700 dark:text-gray-300 truncate">{c.name}</span>
                <span className="text-xs font-mono text-gray-500 dark:text-gray-400 shrink-0">
                  {c.commits}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  )
}
