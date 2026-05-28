import { useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'
import { ReleaseRepoSelectionStep, type RepoSelection } from '../components/wizard/ReleaseRepoSelectionStep'
import { StepSelectTemplate } from './StepSelectTemplate'
import { StepEditNotes } from './StepEditNotes'
import { ReconciliationPanel } from '../../reconciliation/ReconciliationPanel'
import { PreparePagesStep } from './steps/PreparePagesStep'
import { ConflictResolutionDialog } from './ConflictResolutionDialog'
import { useWizardStore } from './store/useWizardStore'

type TemplateDto = components['schemas']['TemplateDto']
type ProjectDetailDto = components['schemas']['ProjectDetailDto']
type ProjectTemplateBindingDto = components['schemas']['ProjectTemplateBindingDto']
type PublishPagesRequest = components['schemas']['PublishPagesRequest']
type PublishResultDto = components['schemas']['PublishResultDto']

type Step = 1 | 2 | 3 | 4 | 5 | 6

const STEP_LABELS: Record<Step, string> = {
  1: 'Select repositories',
  2: 'Select template',
  3: 'Edit notes',
  4: 'Reconcile',
  5: 'Prepare pages',
  6: 'Publish',
}

function StepIndicator({ current }: { current: Step }) {
  return (
    <ol className="flex items-center gap-0 text-xs mb-8 flex-wrap gap-y-2">
      {([1, 2, 3, 4, 5, 6] as Step[]).map((s, i) => {
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
            {i < 5 && (
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
  const [templateId, setTemplateId] = useState<string | null>(null)
  const [releaseId, setReleaseId] = useState<string | null>(null)
  const [initialNotes, setInitialNotes] = useState('')

  // Publish-pages state
  const { pages, resetWizard, markReconciliationStale } = useWizardStore()
  const [showConflictDialog, setShowConflictDialog] = useState(false)
  const [showPublishConfirm, setShowPublishConfirm] = useState(false)
  const [publishError, setPublishError] = useState<string | null>(null)
  const [publishResult, setPublishResult] = useState<PublishResultDto | null>(null)
  const [isPublishing, setIsPublishing] = useState(false)

  const { data: project } = useQuery<ProjectDetailDto>({
    queryKey: ['project', projectId],
    queryFn: () => apiFetch(`/api/v1/projects/${projectId}`).then((r) => r.json()),
    enabled: !!projectId,
  })

  const { data: bindings, isLoading: bindingsLoading } = useQuery<ProjectTemplateBindingDto[]>({
    queryKey: ['project-bindings', projectId],
    queryFn: () => apiFetch(`/api/v1/projects/${projectId}/template-bindings`).then((r) => r.json()),
    enabled: !!projectId,
  })

  const hasReleaseNotesBinding = bindings?.some((b) => b.kind === 'ReleaseNotes') ?? false
  const bindingsReady = !bindingsLoading && bindings !== undefined

  const [templateInitialised, setTemplateInitialised] = useState(false)
  if (project && !templateInitialised) {
    setTemplateId(project.releaseNoteTemplateId ?? null)
    setTemplateInitialised(true)
  }

  const { data: templates = [], isLoading: templatesLoading } = useQuery<TemplateDto[]>({
    queryKey: ['templates'],
    queryFn: () => apiFetch('/api/v1/templates').then((r) => r.json()),
    enabled: step >= 2,
  })

  const createReleaseMutation = useMutation({
    mutationFn: ({ name, selections }: { name: string; selections: RepoSelection[] }) =>
      apiFetch(`/api/v1/projects/${projectId}/releases`, {
        method: 'POST',
        body: JSON.stringify({
          name,
          repositories: selections.map((s) => ({
            repositoryId: s.repositoryId,
            nextVersion: s.nextVersion,
            bumpType: s.bumpType,
          })),
        }),
      }).then((r) => {
        if (!r.ok) throw new Error('Failed to create release')
        return r.json() as Promise<{ id: string; version: string }>
      }),
    onSuccess: (release) => {
      markReconciliationStale()
      setReleaseId(release.id)
      setStep(2)
    },
  })

  const hasConflicts = pages.some((p) => p.draftState.kind === 'conflict')

  const handlePublishPages = async () => {
    if (!releaseId) return
    setIsPublishing(true)
    setPublishError(null)
    try {
      const body: PublishPagesRequest = {
        pages: pages.map((slot) => {
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
          return {
            bindingId: slot.bindingId,
            title: currentTitle,
            body: currentBody,
            parentPageId: null,
            sortOrder: slot.sortOrder,
            linkFromReleaseNotes: false, // resolved server-side from bindings
          }
        }),
      }
      const res = await apiFetch(`/api/v1/releases/${releaseId}/publish-pages`, {
        method: 'POST',
        body: JSON.stringify(body),
      })
      if (!res.ok) {
        const err = await res.json().catch(() => ({}))
        throw new Error(err?.title ?? err?.message ?? 'Publish failed')
      }
      const result: PublishResultDto = await res.json()
      setPublishResult(result)
      resetWizard()
      setTimeout(() => navigate(`/projects/${projectId}/releases/${releaseId}`), 1500)
    } catch (err) {
      setPublishError(err instanceof Error ? err.message : 'Publish failed')
    } finally {
      setIsPublishing(false)
      setShowPublishConfirm(false)
    }
  }

  return (
    <div className="max-w-4xl p-6 space-y-2">
      <nav className="flex items-center gap-2 text-sm text-gray-500 dark:text-gray-400 mb-6">
        <Link to="/projects" className="hover:text-gray-700 dark:hover:text-gray-200">Projects</Link>
        <span>/</span>
        <Link to={`/projects/${projectId}`} className="hover:text-gray-700 dark:hover:text-gray-200">
          {project?.name ?? 'Project'}
        </Link>
        <span>/</span>
        <span className="text-gray-900 dark:text-white font-medium">New release</span>
      </nav>

      <h1 className="text-xl font-semibold text-gray-900 dark:text-white mb-6">Create release</h1>

      <StepIndicator current={step} />

      {/* Step 1 */}
      {step === 1 && project && (
        <ReleaseRepoSelectionStep
          projectId={projectId!}
          repoIds={project.repositories.map((r) => r.repositoryId)}
          onSubmit={(name, selections) => createReleaseMutation.mutate({ name, selections })}
          isSubmitting={createReleaseMutation.isPending}
        />
      )}
      {step === 1 && !project && (
        <p className="text-sm text-gray-500">Loading project…</p>
      )}
      {createReleaseMutation.isError && step === 1 && (
        <p className="text-sm text-red-500 mt-2">Failed to create release. Please try again.</p>
      )}

      {/* Step 2 */}
      {step === 2 && (
        <StepSelectTemplate
          templates={templates}
          isLoading={templatesLoading}
          selectedTemplateId={templateId}
          onSelect={setTemplateId}
          onBack={() => setStep(1)}
          onNext={() => setStep(3)}
        />
      )}

      {/* Step 3 */}
      {step === 3 && releaseId && (
        <StepEditNotes
          releaseId={releaseId}
          initialNotes={initialNotes}
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

      {/* Step 5 — Prepare pages (guard: must have a ReleaseNotes binding) */}
      {step === 5 && releaseId && projectId && bindingsReady && !hasReleaseNotesBinding && (
        <div className="space-y-4">
          <div className="rounded-md bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 px-4 py-4 space-y-2">
            <p className="text-sm font-medium text-amber-800 dark:text-amber-300">
              No Release Notes binding configured
            </p>
            <p className="text-sm text-amber-700 dark:text-amber-400">
              This project needs at least one template binding of kind <strong>Release Notes</strong> before you can prepare pages.
              Go to project settings to add one.
            </p>
          </div>
          <div className="flex gap-3">
            <button
              onClick={() => setStep(4)}
              className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700"
            >
              Back
            </button>
            <button
              onClick={() => navigate(`/settings/projects?tab=pages&projectId=${projectId}`)}
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
            >
              Go to project settings
            </button>
          </div>
        </div>
      )}
      {step === 5 && releaseId && projectId && (bindingsLoading || hasReleaseNotesBinding) && (
        <PreparePagesStep
          releaseId={releaseId}
          projectId={projectId}
          onBack={() => setStep(4)}
          onNext={() => setStep(6)}
        />
      )}

      {/* Step 6 — Publish */}
      {step === 6 && releaseId && (
        <div className="space-y-4">
          {hasConflicts && (
            <div className="rounded-md bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 px-4 py-3 flex items-center justify-between gap-4">
              <p className="text-sm text-amber-800 dark:text-amber-300">
                Some pages have unresolved conflicts from the last re-render.
              </p>
              <button
                onClick={() => setShowConflictDialog(true)}
                className="shrink-0 rounded-md bg-amber-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-amber-700"
              >
                Resolve conflicts
              </button>
            </div>
          )}

          {publishError && (
            <div className="rounded-md bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 px-4 py-3">
              <p className="text-sm text-red-700 dark:text-red-300">{publishError}</p>
            </div>
          )}

          {publishResult && (
            <div className="rounded-md bg-green-50 dark:bg-green-900/30 border border-green-200 dark:border-green-800 px-4 py-3">
              <p className="text-sm text-green-800 dark:text-green-300 font-medium">
                Published {publishResult.publishedPages?.length ?? 0} page(s) to Confluence. Redirecting…
              </p>
            </div>
          )}

          <div className="rounded-lg border border-gray-200 dark:border-gray-700 p-4 space-y-2">
            <p className="text-sm font-medium text-gray-900 dark:text-white">
              Ready to publish {pages.length} page{pages.length !== 1 ? 's' : ''} to Confluence
            </p>
            <ul className="space-y-1">
              {pages.map((slot) => {
                const title =
                  slot.draftState.kind === 'server'
                    ? slot.serverTitle
                    : slot.draftState.kind === 'edited'
                    ? slot.draftState.title
                    : slot.draftState.draftTitle
                return (
                  <li key={slot.bindingId} className="flex items-center gap-2 text-sm text-gray-700 dark:text-gray-300">
                    <span className="text-gray-400">•</span>
                    <span className="truncate">{title}</span>
                    {slot.draftState.kind === 'edited' && (
                      <span className="text-xs text-blue-600 dark:text-blue-400 shrink-0">(edited)</span>
                    )}
                    {slot.draftState.kind === 'conflict' && (
                      <span className="text-xs text-amber-600 dark:text-amber-400 shrink-0">(conflict)</span>
                    )}
                  </li>
                )
              })}
            </ul>
          </div>

          <div className="flex gap-3 pt-2 border-t border-gray-200 dark:border-gray-700">
            <button
              onClick={() => setStep(5)}
              className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700"
            >
              Back
            </button>
            <button
              onClick={() => setShowPublishConfirm(true)}
              disabled={hasConflicts || isPublishing || !!publishResult}
              className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isPublishing ? 'Publishing…' : 'Publish to Confluence'}
            </button>
          </div>
        </div>
      )}

      {/* Publish confirmation dialog */}
      {showPublishConfirm && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
          <div className="w-full max-w-sm rounded-xl bg-white dark:bg-gray-900 shadow-2xl p-6 space-y-4 mx-4">
            <h2 className="text-base font-semibold text-gray-900 dark:text-white">
              Publish {pages.length} page{pages.length !== 1 ? 's' : ''} to Confluence?
            </h2>
            <p className="text-sm text-gray-500 dark:text-gray-400">
              This will create or update Confluence pages. This action can be re-run to update existing pages.
            </p>
            <div className="flex gap-3 justify-end">
              <button
                onClick={() => setShowPublishConfirm(false)}
                className="rounded-md border border-gray-300 dark:border-gray-600 px-4 py-2 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-gray-50 dark:hover:bg-gray-700"
              >
                Cancel
              </button>
              <button
                onClick={handlePublishPages}
                className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700"
              >
                Publish
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Conflict resolution dialog */}
      {showConflictDialog && (
        <ConflictResolutionDialog onClose={() => setShowConflictDialog(false)} />
      )}
    </div>
  )
}
