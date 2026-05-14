import { useEffect, useState } from 'react'
import Handlebars from 'handlebars'

const SAMPLE_DATA = {
  project: { name: 'Apply' },
  version: '1.2.0',
  sections: {
    breaking: [
      { key: 'APPLY-123', title: 'Remove legacy auth flow', commits: [{ message: 'feat!(APPLY-123): remove legacy auth' }] },
    ],
    features: [
      { key: 'APPLY-101', title: 'Add dark mode support', commits: [{ message: 'feat(APPLY-101): add dark mode toggle' }] },
      { key: 'APPLY-104', title: 'Export to CSV', commits: [{ message: 'feat(APPLY-104): add CSV export' }] },
    ],
    fixes: [
      { key: 'APPLY-102', title: 'Fix login redirect loop', commits: [{ message: 'fix(APPLY-102): correct redirect URL' }] },
    ],
    other: [
      { key: 'APPLY-105', title: 'Update dependencies', commits: [{ message: 'chore(APPLY-105): bump deps' }] },
    ],
  },
  contributors: ['alice@example.com', 'bob@example.com', 'carol@example.com'],
  repositories: [
    { name: 'apply-backend', fromTag: '1.1.0', toTag: '1.2.0' },
    { name: 'apply-frontend', fromTag: '1.1.0', toTag: '1.2.0' },
  ],
}

interface Props {
  value: string
  onChange: (value: string) => void
  rows?: number
}

export function TemplateEditor({ value, onChange, rows = 16 }: Props) {
  const [preview, setPreview] = useState('')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    try {
      const compiled = Handlebars.compile(value)
      setPreview(compiled(SAMPLE_DATA))
      setError(null)
    } catch (e) {
      setError((e as Error).message)
    }
  }, [value])

  return (
    <div className="grid grid-cols-2 gap-4">
      {/* ── Editor pane ──────────────────────────────────────────────── */}
      <div className="space-y-1">
        <p className="text-xs font-medium text-gray-500 dark:text-gray-400">Template</p>
        <textarea
          value={value}
          onChange={(e) => onChange(e.target.value)}
          rows={rows}
          spellCheck={false}
          className="w-full rounded-md border border-gray-300 dark:border-gray-600 px-3 py-2 font-mono text-xs bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
          placeholder="# {{project.name}} Release {{version}}&#10;&#10;{{#each sections.features}}&#10;- **{{key}}** {{title}}&#10;{{/each}}"
        />
        {error && (
          <p className="text-xs text-red-500 truncate" title={error}>
            Template error: {error}
          </p>
        )}
      </div>

      {/* ── Preview pane ─────────────────────────────────────────────── */}
      <div className="space-y-1">
        <p className="text-xs font-medium text-gray-500 dark:text-gray-400">
          Preview <span className="font-normal text-gray-400">(sample data)</span>
        </p>
        <pre className="w-full h-full rounded-md border border-gray-200 dark:border-gray-600 px-3 py-2 font-mono text-xs bg-gray-50 dark:bg-gray-800 dark:text-gray-100 overflow-auto whitespace-pre-wrap">
          {error ? <span className="text-red-400 italic">Fix template errors to see preview.</span> : preview || <span className="text-gray-400 italic">Start typing to see preview…</span>}
        </pre>
      </div>
    </div>
  )
}

export const VARIABLE_REFERENCE: { variable: string; description: string }[] = [
  { variable: '{{project.name}}', description: 'Project name' },
  { variable: '{{version}}', description: 'Release version string' },
  { variable: '{{#each sections.breaking}}', description: 'Breaking-change tickets (key, title, commits[])' },
  { variable: '{{#each sections.features}}', description: 'Feature tickets (key, title, commits[])' },
  { variable: '{{#each sections.fixes}}', description: 'Bug-fix tickets (key, title, commits[])' },
  { variable: '{{#each sections.other}}', description: 'Other tickets (key, title, commits[])' },
  { variable: '{{#each contributors}}', description: 'Contributor email addresses' },
  { variable: '{{#each repositories}}', description: 'Repositories (name, fromTag, toTag)' },
  { variable: '{{key}}', description: 'Jira ticket key (inside an each block)' },
  { variable: '{{title}}', description: 'Ticket title (inside an each block)' },
  { variable: '{{message}}', description: 'Commit message (inside commits each block)' },
]
