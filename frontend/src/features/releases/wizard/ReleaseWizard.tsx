import { useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'
import { StepConfirmRange, buildSemverSuggestion } from './StepConfirmRange'
import { StepSelectTemplate } from './StepSelectTemplate'
import { StepEditNotes } from './StepEditNotes'
import { ReconciliationPanel } from '../../reconciliation/ReconciliationPanel'
import { StepPublish } from './StepPublish'

type ProjectChangesDto = components['schemas']['ProjectChangesDto']
type TemplateDto = components['schemas']['TemplateDto']
type ProjectDetailDto = components['schemas']['ProjectDetailDto']
type ReleaseDetailDto = components['schemas']['ReleaseDetailDto']

type Step = 1 | 2 | 3 | 4 | 5

const STEP_LABELS: Record<Step, string> = {
  1: 'Confirm range',
  2: 'Select template',
  3: 'Edit notes',
  4: 'Reconcile',
  5: 'Publish',
}

function StepIndicator({ current }: { current: Step }) {
  return (
    <ol className="flex items-center gap-0 text-xs mb-8">
      {([1, 2, 3, 4, 5] as Step[]).map((s, i) => {
        const done = s < current
        const active = s === current
        return (
          <li key={s} className="flex items-center">
            <span
              className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold shrink-0 ${
                done
                  ? 'bg-blue-600 text-white'
                  : active
                  ? 'border-2 border-blue-600 text-blue-600'
                  : 'border border-gray-300 dark:border-gray-600 text-gray-400'
              }`}
            >
              {done ? '✓' : s}
            </span>
            <span
              className={`ml-1.5 hidden sm:inline ${
                active ? 'text-gray-900 dark:text-white font-medium' : 'text-gray-400 dark:text-gray-500'
              }`}
            >
              {STEP_LABELS[s]}
            </span>
            {i < 4 && (
              <span className="mx-3 h-px w-8 bg-gray-200 dark:bg-gray-700 shrink-0" />
            )}
          </li>
        )
      })}
    </ol>
  )
}

export function ReleaseWizard() {
  const { id: projectId } = useParams<{ id: string }>()
  const navigate = useNavigate()

  const [step, setStep] = useState<Step>(1)
  const [version, setVersion] = useState('')
  const [templateId, setTemplateId] = useState<string | null>(null)
  const [releaseId, setReleaseId] = useState<string | null>(null)

  // Load project detail (to find default template)
  const { data: project } = useQuery<ProjectDetailDto>({
    queryKey: ['project', projectId],
    queryFn: () => apiFetch(`/api/v1/projects/${projectId}`).then((r) => r.json()),
    enabled: !!projectId,
  })

  // Load project changes (for range rows + semver suggestion)
  const { data: changes, isLoading: changesLoading } = useQuery<ProjectChangesDto>({
    queryKey: ['project-changes', projectId],
    queryFn: () => apiFetch(`/api/v1/projects/${projectId}/changes`).then((r) => r.json()),
    enabled: !!projectId,
    select: (data) => {
      // Compute semver suggestion once on first load
      return data
    },
  })

  // Pre-populate version from semver suggestion when changes arrive
  const [versionInitialised, setVersionInitialised] = useState(false)
  if (changes && !versionInitialised) {
    const suggested = buildSemverSuggestion(
      changes.repositories,
      changes.repositories.map((r) => r.fromTag),
    )
    setVersion(suggested)
    setVersionInitialised(true)
  }

  // Pre-select project default template when project loads
  const [templateInitialised, setTemplateInitialised] = useState(false)
  if (project && !templateInitialised) {
    setTemplateId(project.releaseNoteTemplateId ?? null)
    setTemplateInitialised(true)
  }

  // Load templates for step 2
  const { data: templates = [], isLoading: templatesLoading } = useQuery<TemplateDto[]>({
    queryKey: ['templates'],
    queryFn: () => apiFetch('/api/v1/templates').then((r) => r.json()),
    enabled: step >= 2,
  })

  // Create draft release mutation (called between step 2 and step 3)
  const createReleaseMutation = useMutation({
    mutationFn: () => {
      const ranges =
        changes?.repositories.map((r) => ({
          repositoryId: r.repositoryId,
          fromTag: r.fromTag,
          toTag: r.toTag,
        })) ?? []
      return apiFetch(`/api/v1/projects/${projectId}/releases`, {
        method: 'POST',
        body: JSON.stringify({ version, templateId, repositoryTags: ranges }),
      }).then((r) => {
        if (!r.ok) throw new Error('Failed to create release')
        return r.json() as Promise<ReleaseDetailDto>
      })
    },
    onSuccess: (release) => {
      setReleaseId(release.id)
      setStep(3)
    },
  })

  const ranges =
    changes?.repositories.map((r) => ({
      repositoryId: r.repositoryId,
      repositoryName: r.repositoryName,
      fromTag: r.fromTag,
      toTag: r.toTag,
      commitCount: r.summary.commitCount,
    })) ?? []

  // Initial notes come from the created release's generatedNotesMarkdown
  const [initialNotes, setInitialNotes] = useState('')

  if (changesLoading) {
    return (
      <div className="p-8">
        <p className="text-sm text-gray-500">Loading project changes…</p>
      </div>
    )
  }

  return (
    <div className="max-w-4xl p-6 space-y-2">
      {/* Breadcrumb */}
      <nav className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 mb-6">
        <Link to="/projects" className="hover:text-gray-700 dark:hover:text-gray-200">Projects</Link>
        <span>/</span>
        <Link to={`/projects/${projectId}`} className="hover:text-gray-700 dark:hover:text-gray-200">
          {changes?.projectName ?? 'Project'}
        </Link>
        <span>/</span>
        <span className="text-gray-900 dark:text-white font-medium">New release</span>
      </nav>

      <h1 className="text-xl font-semibold text-gray-900 dark:text-white mb-6">Create release</h1>

      <StepIndicator current={step} />

      {/* Step 1 */}
      {step === 1 && (
        <StepConfirmRange
          version={version}
          onVersionChange={setVersion}
          ranges={ranges}
          onNext={() => setStep(2)}
        />
      )}

      {/* Step 2 */}
      {step === 2 && (
        <>
          <StepSelectTemplate
            templates={templates}
            isLoading={templatesLoading}
            selectedTemplateId={templateId}
            onSelect={setTemplateId}
            onBack={() => setStep(1)}
            onNext={() => createReleaseMutation.mutate()}
          />
          {createReleaseMutation.isPending && (
            <p className="text-sm text-gray-500 mt-2">Creating release draft…</p>
          )}
          {createReleaseMutation.isError && (
            <p className="text-sm text-red-500 mt-2">Failed to create release. Please try again.</p>
          )}
        </>
      )}

      {/* Step 3 */}
      {step === 3 && releaseId && (
        <StepEditNotes
          releaseId={releaseId}
          initialNotes={initialNotes || ''}
          onBack={() => setStep(2)}
          onNext={(saved) => {
            setInitialNotes(saved)
            setStep(4)
          }}
        />
      )}

      {/* Step 4 */}
      {step === 4 && releaseId && (
        <ReconciliationPanel
          releaseId={releaseId}
          onBack={() => setStep(3)}
          onNext={() => setStep(5)}
        />
      )}

      {/* Step 5 */}
      {step === 5 && releaseId && (
        <StepPublish
          releaseId={releaseId}
          version={version}
          onBack={() => setStep(4)}
          onPublished={(release) => {
            setTimeout(() => navigate(`/releases/${release.id}`), 1500)
          }}
        />
      )}
    </div>
  )
}
