const CHANGE_TYPES = ['feat', 'fix', 'docs', 'style', 'refactor', 'perf', 'test', 'build', 'ci', 'chore', 'revert']

interface ChangeFiltersProps {
  type: string
  setType: (t: string) => void
  search: string
  setSearch: (s: string) => void
}

export function ChangeFilters({ type, setType, search, setSearch }: ChangeFiltersProps) {
  const hasFilters = type !== '' || search !== ''

  return (
    <div className="flex flex-wrap items-center gap-3">
      <input
        type="search"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Search by ticket ID…"
        className="w-52 rounded-md border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
      />
      <select
        value={type}
        onChange={(e) => setType(e.target.value)}
        className="rounded-md border border-gray-300 dark:border-gray-600 px-3 py-1.5 text-sm bg-white dark:bg-gray-700 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500"
      >
        <option value="">All types</option>
        {CHANGE_TYPES.map((t) => (
          <option key={t} value={t}>
            {t}
          </option>
        ))}
      </select>
      {hasFilters && (
        <button
          onClick={() => { setType(''); setSearch('') }}
          className="text-xs text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-200 underline"
        >
          Clear filters
        </button>
      )}
    </div>
  )
}
