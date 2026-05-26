import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'
import type { components } from '../../../../lib/api'

type PreparedPageDto = components['schemas']['PreparedPageDto']
type ReconciliationSummaryDto = components['schemas']['ReconciliationSummaryDto']

export type DraftState =
  | { kind: 'server' }
  | { kind: 'edited'; title: string; body: string }
  | { kind: 'conflict'; serverTitle: string; serverBody: string; draftTitle: string; draftBody: string }

export interface PreparedPageSlot {
  bindingId: string
  serverTitle: string
  serverBody: string
  unknownTokens: string[]
  sortOrder: number
  draftState: DraftState
}

interface ReconciliationSlice {
  ran: boolean
  stale: boolean
  data: ReconciliationSummaryDto | null
}

interface WizardState {
  projectId: string | null
  releaseId: string | null
  pages: PreparedPageSlot[]
  reconciliation: ReconciliationSlice

  initPages(releaseId: string, pages: PreparedPageDto[]): void
  editPage(bindingId: string, title: string, body: string): void
  reRenderPages(freshPages: PreparedPageDto[]): void
  resolveConflict(bindingId: string, resolution: 'keep' | 'discard'): void
  setReconciliationData(data: ReconciliationSummaryDto): void
  markReconciliationStale(): void
  resetWizard(): void
}

const INITIAL_RECONCILIATION: ReconciliationSlice = { ran: false, stale: false, data: null }

export const useWizardStore = create<WizardState>()(
  persist(
    (set) => ({
      projectId: null,
      releaseId: null,
      pages: [],
      reconciliation: INITIAL_RECONCILIATION,

      initPages(releaseId, pages) {
        set({
          releaseId,
          pages: pages.map((p) => ({
            bindingId: p.bindingId,
            serverTitle: p.title,
            serverBody: p.body,
            unknownTokens: p.unknownTokens,
            sortOrder: p.sortOrder,
            draftState: { kind: 'server' },
          })),
        })
      },

      editPage(bindingId, title, body) {
        set((state) => ({
          pages: state.pages.map((p) =>
            p.bindingId !== bindingId
              ? p
              : { ...p, draftState: { kind: 'edited', title, body } }
          ),
        }))
      },

      // Merge fresh server render into existing slots:
      //   server  → server  (replace server content)
      //   edited  → conflict (preserve draft, surface fresh as server side)
      //   conflict → conflict (update server side, keep existing draft)
      reRenderPages(freshPages) {
        set((state) => {
          const freshMap = new Map(freshPages.map((p) => [p.bindingId, p]))
          const merged = state.pages.map((slot) => {
            const fresh = freshMap.get(slot.bindingId)
            if (!fresh) return slot

            const updatedSlot: PreparedPageSlot = {
              ...slot,
              serverTitle: fresh.title,
              serverBody: fresh.body,
              unknownTokens: fresh.unknownTokens,
              sortOrder: fresh.sortOrder,
            }

            if (slot.draftState.kind === 'server') {
              updatedSlot.draftState = { kind: 'server' }
            } else if (slot.draftState.kind === 'edited') {
              updatedSlot.draftState = {
                kind: 'conflict',
                serverTitle: fresh.title,
                serverBody: fresh.body,
                draftTitle: slot.draftState.title,
                draftBody: slot.draftState.body,
              }
            } else {
              // already conflict — update the server side, keep the draft
              updatedSlot.draftState = {
                kind: 'conflict',
                serverTitle: fresh.title,
                serverBody: fresh.body,
                draftTitle: slot.draftState.draftTitle,
                draftBody: slot.draftState.draftBody,
              }
            }

            return updatedSlot
          })

          // Append any new pages the server returned that don't exist yet
          const existingIds = new Set(state.pages.map((p) => p.bindingId))
          const newSlots = freshPages
            .filter((p) => !existingIds.has(p.bindingId))
            .map((p): PreparedPageSlot => ({
              bindingId: p.bindingId,
              serverTitle: p.title,
              serverBody: p.body,
              unknownTokens: p.unknownTokens,
              sortOrder: p.sortOrder,
              draftState: { kind: 'server' },
            }))

          return { pages: [...merged, ...newSlots] }
        })
      },

      // keep  → edited (preserve the user's draft)
      // discard → server (use the fresh server version)
      resolveConflict(bindingId, resolution) {
        set((state) => ({
          pages: state.pages.map((p) => {
            if (p.bindingId !== bindingId || p.draftState.kind !== 'conflict') return p
            const { serverTitle, serverBody, draftTitle, draftBody } = p.draftState
            return {
              ...p,
              draftState:
                resolution === 'keep'
                  ? { kind: 'edited', title: draftTitle, body: draftBody }
                  : { kind: 'server' },
              serverTitle,
              serverBody,
            }
          }),
        }))
      },

      setReconciliationData(data) {
        set({ reconciliation: { ran: true, stale: false, data } })
      },

      markReconciliationStale() {
        set((state) => {
          if (!state.reconciliation.ran) return state
          return { reconciliation: { ...state.reconciliation, stale: true } }
        })
      },

      resetWizard() {
        set({ projectId: null, releaseId: null, pages: [], reconciliation: INITIAL_RECONCILIATION })
      },
    }),
    {
      name: 'wizard-storage',
      storage: createJSONStorage(() => sessionStorage),
    }
  )
)
