import { useState } from 'react'
import { useParams, useNavigate, Link } from 'react-router-dom'
import { useQuery, useMutation } from '@tanstack/react-query'
import { apiFetch } from '../../../lib/apiClient'
import type { components } from '../../../lib/api'
import { ReleaseRepoSelectionStep, type RepoSelection } from '../components/wizard/ReleaseRepoSelectionStep'
import { StepSelectTemplate } from './StepSelectTemplate'
import { StepEditNotes } from './StepEditNotes'
import { ReconciliationPanel } from '../../reconciliation/ReconciliationPanel'
import { StepPublish } from './StepPublish'

type TemplateDto = components['schemas']['TemplateDto']
type ProjectDetailDto = components['schemas']['ProjectDetailDto']

type Step = 1 | 2 | 3 | 4 | 5

const STEP_LABELS: Record<Step, string> = {
  1: 'Select repositories',
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
  const [templateId, setTemplateId] = useState<string | null>(null)
  const [releaseId, setReleaseId] = useState<string | null>(null)
  const [releaseVersion, setReleaseVersion] = useState('')
  const [initialNotes, setInitialNotes] = useState('')

  const { data: project } = useQuery<ProjectDetailDto>({
    queryKey: ['project', projectId],
    queryFn: () => apiFetch(`/api/v1/projects/${projectId}`).then((r) => r.json()),
    enabled: !!projectId,
  })

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
      setReleaseId(release.id)
      setReleaseVersion(release.version)
      setStep(2)
    },
  })

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

      {/* Step 5 */}
      {step === 5 && releaseId && (
        <StepPublish
          releaseId={releaseId}
          version={releaseVersion}
          onBack={() => setStep(4)}
          onPublished={(release) => {
            setTimeout(() => navigate(`/releases/${release.id}`), 1500)
          }}
        />
      )}
    </div>
  )
}
