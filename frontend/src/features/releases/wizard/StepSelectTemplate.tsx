import type { components } from '../../../lib/api'

type TemplateDto = components['schemas']['TemplateDto']

interface StepSelectTemplateProps {
  templates: TemplateDto[]
  isLoading: boolean
  selectedTemplateId: string | null
  onSelect: (id: string | null) => void
  onBack: () => void
  onNext: () => void
}

export function StepSelectTemplate({
  templates,
  isLoading,
  selectedTemplateId,
  onSelect,
  onBack,
  onNext,
}: StepSelectTemplateProps) {
  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-base font-semibold text-gray-900 dark:text-white">Step 2 — Select release note template</h2>
        <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
          Choose a Handlebars template to generate the initial release notes. The project default is pre-selected.
        </p>
      </div>

      {isLoading ? (
        <p className="text-sm text-gray-500">Loading templates…</p>
      ) : templates.length === 0 ? (
        <div className="rounded-lg border border-dashed border-gray-300 dark:border-gray-600 p-6 text-center">
          <p className="text-sm text-gray-500 dark:text-gray-400">No templates found.</p>
          <p className="text-xs text-gray-400 mt-1">
            Create one in Settings → Templates, or proceed without a template.
          </p>
        </div>
      ) : (
        <ul className="space-y-2">
          {templates.map((t) => (
            <li key={t.id}>
              <label
                className={`flex items-start gap-3 rounded-lg border p-4 cursor-pointer transition-colors ${
                  selectedTemplateId === t.id
                    ? 'border-blue-500 bg-blue-50 dark:bg-blue-950/30 dark:border-blue-600'
                    : 'border-gray-200 dark:border-gray-700 hover:border-blue-300 dark:hover:border-blue-700'
                }`}
              >
                <input
                  type="radio"
                  name="template"
                  value={t.id}
                  checked={selectedTemplateId === t.id}
                  onChange={() => onSelect(t.id)}
                  className="mt-0.5 accent-blue-600"
                />
                <div className="min-w-0">
                  <p className="text-sm font-medium text-gray-900 dark:text-white">{t.name}</p>
                  <p className="mt-0.5 text-xs text-gray-400 dark:text-gray-500 font-mono truncate">
                    {t.contentTemplate.slice(0, 80)}{t.contentTemplate.length > 80 ? '…' : ''}
                  </p>
                </div>
              </label>
            </li>
          ))}
        </ul>
      )}

      {/* No template option */}
      <label
        className={`flex items-center gap-3 rounded-lg border p-4 cursor-pointer transition-colors ${
          selectedTemplateId === null
            ? 'border-blue-500 bg-blue-50 dark:bg-blue-950/30 dark:border-blue-600'
            : 'border-gray-200 dark:border-gray-700 hover:border-blue-300 dark:hover:border-blue-700'
        }`}
      >
        <input
          type="radio"
          name="template"
          value=""
          checked={selectedTemplateId === null}
          onChange={() => onSelect(null)}
          className="accent-blue-600"
        />
        <span className="text-sm text-gray-600 dark:text-gray-400 italic">No template — start with blank notes</span>
      </label>

      <div className="flex justify-between">
        <button
          onClick={onBack}
          className="px-5 py-2 rounded-md border border-gray-300 dark:border-gray-600 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-800 transition-colors"
        >
          Back
        </button>
        <button
          onClick={onNext}
          className="px-5 py-2 rounded-md bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 transition-colors"
        >
          Next: Edit notes
        </button>
      </div>
    </div>
  )
}
