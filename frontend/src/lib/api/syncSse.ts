import { useAuthStore } from '../authStore'

export type SseEventType = 'repo_started' | 'step_changed' | 'repo_completed' | 'project_complete'

export interface RepoStartedPayload {
  repoId: string
  repoName: string
  syncId: string
  totalRepos: number
  completedCount: number
}

export interface StepChangedPayload {
  repoId: string
  syncId: string
  currentStep: string
  elapsedMs: number
}

export interface RepoCompletedPayload {
  repoId: string
  syncId: string
  status: string
  commitCount: number
  ticketCount: number
  breakingChangeCount: number
  contributorCount: number
  errorMessage: string | null
}

export interface ProjectCompletePayload {
  projectSyncId: string
  status: string
  succeededCount: number
  failedCount: number
  skippedCount: number
  completedAt: string | null
}

export type SseCallbacks = {
  onRepoStarted?: (payload: RepoStartedPayload) => void
  onStepChanged?: (payload: StepChangedPayload) => void
  onRepoCompleted?: (payload: RepoCompletedPayload) => void
  onComplete?: (payload: ProjectCompletePayload) => void
  onError?: (err: Event) => void
}

export function openProjectSyncStream(
  projectId: string,
  callbacks: SseCallbacks,
  lastEventId?: string,
): () => void {
  const token = useAuthStore.getState().accessToken
  const url = `/api/v1/projects/${projectId}/sync/active/stream`

  // EventSource doesn't support custom headers; pass token via cookie or query param fallback
  // We use a polyfill approach: fetch-based SSE via a ReadableStream
  let aborted = false
  const controller = new AbortController()

  const headers: Record<string, string> = {
    Accept: 'text/event-stream',
    'Cache-Control': 'no-cache',
  }
  if (token) headers['Authorization'] = `Bearer ${token}`
  if (lastEventId) headers['Last-Event-ID'] = lastEventId

  void (async () => {
    let retryCount = 0
    let lastId = lastEventId

    while (!aborted && retryCount < 3) {
      try {
        const resp = await fetch(url, {
          signal: controller.signal,
          headers: { ...headers, ...(lastId ? { 'Last-Event-ID': lastId } : {}) },
        })

        if (resp.status === 204) break
        if (!resp.ok || !resp.body) { retryCount++; continue }

        retryCount = 0
        const reader = resp.body.getReader()
        const decoder = new TextDecoder()
        let buffer = ''

        while (!aborted) {
          const { done, value } = await reader.read()
          if (done) break
          buffer += decoder.decode(value, { stream: true })
          const messages = buffer.split('\n\n')
          buffer = messages.pop() ?? ''

          for (const raw of messages) {
            if (!raw.trim()) continue
            const lines = raw.split('\n')
            let id = '', event = '', data = ''
            for (const line of lines) {
              if (line.startsWith('id: ')) id = line.slice(4)
              else if (line.startsWith('event: ')) event = line.slice(7)
              else if (line.startsWith('data: ')) data = line.slice(6)
            }
            if (id) lastId = id
            dispatchEvent(event as SseEventType, data, callbacks)
            if (event === 'project_complete') { aborted = true; break }
          }
        }
      } catch {
        if (aborted) break
        retryCount++
        await new Promise((r) => setTimeout(r, 1000 * retryCount))
      }
    }
  })()

  return () => {
    aborted = true
    controller.abort()
  }
}

function dispatchEvent(event: SseEventType, data: string, cb: SseCallbacks) {
  try {
    const payload = JSON.parse(data)
    if (event === 'repo_started') cb.onRepoStarted?.(payload)
    else if (event === 'step_changed') cb.onStepChanged?.(payload)
    else if (event === 'repo_completed') cb.onRepoCompleted?.(payload)
    else if (event === 'project_complete') cb.onComplete?.(payload)
  } catch { /* malformed SSE data */ }
}
