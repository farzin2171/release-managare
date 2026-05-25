import { create } from 'zustand'
import { persist, createJSONStorage } from 'zustand/middleware'
import type { components } from '../../../../lib/api'

type PreparedPageDto = components['schemas']['PreparedPageDto']

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

interface WizardState {
  projectId: string | null
  releaseId: string | null
  pages: PreparedPageSlot[]

  initPages(releaseId: string, pages: PreparedPageDto[]): void
  editPage(bindingId: string, title: string, body: string): void
  resetWizard(): void
}

export const useWizardStore = create<WizardState>()(
  persist(
    (set) => ({
      projectId: null,
      releaseId: null,
      pages: [],

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

      resetWizard() {
        set({ projectId: null, releaseId: null, pages: [] })
      },
    }),
    {
      name: 'wizard-storage',
      storage: createJSONStorage(() => sessionStorage),
    }
  )
)
