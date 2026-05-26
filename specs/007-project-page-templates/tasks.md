# Tasks: Project Page Templates

**Input**: Design documents from `specs/007-project-page-templates/`
**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/ ✅ | quickstart.md ✅

**Tests**: Included — TDD mandated by project constitution (Principle III) and explicitly required in quickstart.md Step 2.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1–US4)
- Exact file paths included in all descriptions

---

## Phase 1: Setup — Domain Layer

**Purpose**: Add new domain entities and enums that have zero external dependencies. All subsequent phases depend on this phase being complete.

- [X] T001 [P] Create `ProjectTemplateBinding` POCO entity (all fields from data-model.md) in `backend/src/RepoManager.Domain/Entities/ProjectTemplateBinding.cs`
- [X] T002 [P] Create `ProjectCustomVariable` POCO entity (all fields from data-model.md) in `backend/src/RepoManager.Domain/Entities/ProjectCustomVariable.cs`
- [X] T003 [P] Create `TemplateBindingKind` enum (`ReleaseNotes | Checklist | Custom`) in `backend/src/RepoManager.Domain/Enums/TemplateBindingKind.cs`
- [X] T004 [P] Create `VersionBumpStrategy` enum (`Patch | Minor | Major`) in `backend/src/RepoManager.Domain/Enums/VersionBumpStrategy.cs`
- [X] T005 Modify `Project` entity: add `VersionBumpStrategy` property (default `Minor`), add `ICollection<ProjectTemplateBinding>` and `ICollection<ProjectCustomVariable>` navigation properties, remove `DefaultReleaseNoteTemplateId` property in `backend/src/RepoManager.Domain/Entities/Project.cs`

**Checkpoint**: `dotnet build RepoManager.Domain` passes with zero errors.

---

## Phase 2: Foundational — Application Layer, Infrastructure, Migration

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented. Establishes service contracts, DTOs, EF Core schema, and Handlebars rendering infrastructure.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T006 Create `IProjectTemplateBindingService` interface (5 methods: GetAllAsync, CreateAsync, UpdateAsync, DeleteAsync, ReorderAsync) in `backend/src/RepoManager.Application/Services/IProjectTemplateBindingService.cs`
- [X] T007 [P] Create `IProjectCustomVariableService` interface (3 methods: GetAllAsync, UpsertAsync, DeleteAsync) in `backend/src/RepoManager.Application/Services/IProjectCustomVariableService.cs`
- [X] T008 [P] Create `IReleaseRenderService` interface (3 methods: PrepareAsync, PublishAsync, PreviewTemplateAsync) in `backend/src/RepoManager.Application/Services/IReleaseRenderService.cs`
- [X] T009 Create binding DTOs as C# records (`ProjectTemplateBindingDto`, `CreateBindingRequest`, `UpdateBindingRequest`) in `backend/src/RepoManager.Application/DTOs/Bindings/`
- [X] T010 [P] Create custom variable DTO as C# record (`ProjectCustomVariableDto`) in `backend/src/RepoManager.Application/DTOs/CustomVariables/ProjectCustomVariableDto.cs`
- [X] T011 [P] Create render context DTOs as C# records (`ReleaseRenderContextDto`, `RepoContextDto`, `TicketBucketsDto`, `TicketDto`, `ContributorDto`, `ReconciliationSummaryDto`, `ConfluenceTargetDto`, `ProjectInfoDto`) in `backend/src/RepoManager.Application/DTOs/Releases/`
- [X] T012 [P] Create prepared page and publish DTOs as C# records (`PreparedPageDto`, `PreparedReleaseDto`, `PreparePageRequest`, `PublishPagesRequest`, `PublishPageDto`, `PublishResultDto`, `PublishedPageDto`, `TemplatePreviewRequest`, `TemplatePreviewDto`) in `backend/src/RepoManager.Application/DTOs/Releases/`
- [X] T013 Create `CreateBindingRequestValidator` (TemplateId > 0, Kind enum, PageTitleTemplate 1–500 chars, SortOrder ≥ 0) and `UpdateBindingRequestValidator` in `backend/src/RepoManager.Application/Validators/`
- [X] T014 [P] Create `PublishPagesRequestValidator` (Pages non-empty, each Title 1–255 chars, each BindingId > 0) and `ProjectCustomVariableUpsertValidator` (Key 1–50 chars matches `[a-zA-Z][a-zA-Z0-9_]*`, Value ≤ 500 chars) in `backend/src/RepoManager.Application/Validators/`
- [X] T015 Create `ProjectTemplateBindingConfiguration` EF Core config (HasCheckConstraint Kind IN, HasIndex ProjectId+SortOrder non-unique, FK RESTRICT on TemplateId delete, FK CASCADE on ProjectId delete) in `backend/src/RepoManager.Infrastructure/Persistence/EntityConfigurations/ProjectTemplateBindingConfiguration.cs`
- [X] T016 [P] Create `ProjectCustomVariableConfiguration` EF Core config (unique index on ProjectId+Key, HasCheckConstraint Key GLOB pattern, FK CASCADE on ProjectId delete) in `backend/src/RepoManager.Infrastructure/Persistence/EntityConfigurations/ProjectCustomVariableConfiguration.cs`
- [X] T017 Update `AppDbContext` to add `DbSet<ProjectTemplateBinding> TemplateBindings` and `DbSet<ProjectCustomVariable> CustomVariables` in `backend/src/RepoManager.Infrastructure/Persistence/AppDbContext.cs`
- [X] T018 Generate EF Core migration `AddProjectTemplateBindings` then hand-edit `Up()` to insert data backfill SQL (ProjectTemplateBinding of kind ReleaseNotes per project with non-null DefaultReleaseNoteTemplateId) and `DROP COLUMN DefaultReleaseNoteTemplateId` within the same transaction; implement `Down()` reversal in `backend/src/RepoManager.Infrastructure/Persistence/Migrations/`
- [X] T019 Create `MissingTokenRecorder` implementing `IFormatterProvider` with `[ThreadStatic]` `HashSet<string>` capture bag; expose `BeginCapture()` / `EndCapture()` methods; override custom-dict indexer to record `"custom.<key>"` for missing keys in `backend/src/RepoManager.Infrastructure/Services/Handlebars/MissingTokenRecorder.cs`
- [X] T020 [P] Create `HandlebarsHelpers` static class registering 9 block/inline helpers (`formatDate`, `length`, `eq`, `gt`, `minus`, `lower`, `upper`, `truncate`, `jiraLink`) via `IHandlebars.RegisterHelper` in `backend/src/RepoManager.Infrastructure/Services/Handlebars/HandlebarsHelpers.cs`
- [X] T021 Create `HandlebarsFactory` that builds and caches the `IHandlebars` singleton with `MissingTokenRecorder` and all helpers registered; add DI registration (`AddSingleton<MissingTokenRecorder>`, `AddSingleton<IHandlebars>(...)`) in `backend/src/RepoManager.Infrastructure/Services/Handlebars/HandlebarsFactory.cs` and `backend/src/RepoManager.Api/Program.cs`

**Checkpoint**: `dotnet build backend/src` passes; all interfaces, DTOs, and validators compile.

---

## Phase 3: User Story 1 — Bind Multiple Auto-Filled Page Templates (Priority: P1) 🎯 MVP

**Goal**: Admin binds templates to a project with custom variables, runs the release wizard, and two auto-filled Confluence pages are created.

**Independent Test**: An admin can bind two templates to a project, configure custom variables, run the release wizard, and end up with two published Confluence pages with auto-filled values, without touching any other user story.

### TDD: Write failing tests first (run `dotnet test --filter Category=Unit` — all must fail RED)

- [X] T022 [US1] Write failing unit tests for `ReleaseRenderService`: `RenderContext_WithUnknownToken_RendersEmptyString`, `RenderContext_WithUnknownToken_CapturesTokenName`, `PreparePages_WhenNoSemverTag_ThrowsValidationException`, `PreparePages_WhenNoReleaseNotesBinding_ThrowsValidationException`, `BuildContext_PrimaryRepoHasReleaseRepositoryRow_UsesSnapshotVersion`, `BuildContext_NoPrimaryRepoSnapshot_AppliesBumpStrategy` in `backend/tests/RepoManager.UnitTests/Services/ReleaseRenderServiceTests.cs`
- [X] T023 [P] [US1] Write failing unit tests for `HandlebarsHelpers`: one test per helper covering `formatDate`, `length`, `eq`, `gt`, `minus`, `lower`, `upper`, `truncate`, `jiraLink` in `backend/tests/RepoManager.UnitTests/Services/HandlebarsHelpersTests.cs`
- [X] T024 [P] [US1] Write failing unit tests for `ProjectTemplateBindingService`: `Delete_LastReleaseNotesBinding_ThrowsConflictException`, `Create_SecondReleaseNotesBinding_ThrowsConflictException` in `backend/tests/RepoManager.UnitTests/Services/ProjectTemplateBindingServiceTests.cs`

### Backend Implementation

- [X] T025 [US1] Implement `ReleaseRenderService.BuildContextAsync` (load Release + project + bindings + custom vars, resolve version from ReleaseRepository snapshot or tag+VersionBumpStrategy fallback, assemble `ReleaseRenderContextDto`) and `PrepareAsync` (render each binding title+body with BeginCapture/EndCapture, validate title lengths, detect title collisions, return `PreparedReleaseDto`) in `backend/src/RepoManager.Infrastructure/Services/ReleaseRenderService.cs`
- [X] T026 [US1] Implement `ProjectTemplateBindingService.GetAllAsync`, `CreateAsync` (enforce single-ReleaseNotes constraint), `UpdateAsync`, `DeleteAsync` (enforce last-ReleaseNotes constraint) with audit log calls in `backend/src/RepoManager.Infrastructure/Services/ProjectTemplateBindingService.cs`
- [X] T027 [P] [US1] Implement `ProjectCustomVariableService.GetAllAsync`, `UpsertAsync` (insert-or-update under unique index on ProjectId+Key), `DeleteAsync` in `backend/src/RepoManager.Infrastructure/Services/ProjectCustomVariableService.cs`
- [X] T028 [US1] Create `ProjectTemplateBindingsController` with `GET /projects/{projectId}/template-bindings`, `POST /projects/{projectId}/template-bindings`, `PUT /projects/{projectId}/template-bindings/{bindingId}`, `DELETE /projects/{projectId}/template-bindings/{bindingId}` endpoints (Viewer for GET, Admin for write) in `backend/src/RepoManager.Api/Controllers/ProjectTemplateBindingsController.cs`
- [X] T029 [P] [US1] Create `ProjectCustomVariablesController` with `GET /projects/{projectId}/custom-variables`, `PUT /projects/{projectId}/custom-variables/{key}`, `DELETE /projects/{projectId}/custom-variables/{key}` endpoints in `backend/src/RepoManager.Api/Controllers/ProjectCustomVariablesController.cs`
- [X] T030 [US1] Add `POST /releases/{releaseId}/prepare-pages` endpoint to `ReleasesController` mapping to `IReleaseRenderService.PrepareAsync`; register `IProjectTemplateBindingService`, `IProjectCustomVariableService`, `IReleaseRenderService` as scoped in `backend/src/RepoManager.Api/Controllers/ReleasesController.cs` and `Program.cs`

**Gate**: Unit tests from T022–T024 pass GREEN. `dotnet run` starts; Swagger shows all new GET/POST/PUT/DELETE endpoints.

### Frontend Implementation

- [X] T031 [US1] Regenerate OpenAPI client (`npm run generate-api` from `frontend/`) and verify new types appear (`ProjectTemplateBindingDto`, `PreparedReleaseDto`, `PublishResultDto`) in `frontend/src/lib/api/`
- [X] T032 [US1] Create `useProjectBindings` TanStack Query hook (list, create mutation, update mutation, delete mutation) in `frontend/src/features/settings/projects/hooks/useProjectBindings.ts`
- [X] T033 [P] [US1] Create `useProjectCustomVariables` TanStack Query hook (list, upsert mutation, delete mutation) in `frontend/src/features/settings/projects/hooks/useProjectCustomVariables.ts`
- [X] T034 [US1] Create `ProjectPagesTab` with shadcn `<Table>` listing bindings (kind badge, page-title template, parent page, link flag, sort order), add/edit/delete actions, loading skeleton, and toast on success/error in `frontend/src/features/settings/projects/ProjectPagesTab.tsx`
- [X] T035 [P] [US1] Create `BindingFormSheet` using React Hook Form + Zod with all `CreateBindingRequest` fields (template selector, kind select, pageTitleTemplate input, parentPageId input, linkFromReleaseNotes toggle, sortOrder input) in `frontend/src/features/settings/projects/BindingFormSheet.tsx`
- [X] T036 [P] [US1] Create `CustomVariablesSection` with inline key/value table, add/edit/delete rows, shows destructive `<Alert>` when trying to use reserved key patterns in `frontend/src/features/settings/projects/CustomVariablesSection.tsx`
- [X] T037 [US1] Wire `ProjectPagesTab` and `CustomVariablesSection` into the existing project settings page as a new "Pages" tab in `frontend/src/features/settings/projects/`
- [X] T038 [US1] Create `useWizardStore` Zustand store persisted to `sessionStorage`: `DraftState` discriminated union (`server | edited | conflict`), state shape from `service-interfaces.md`, actions `initPages`, `editPage`, `resetWizard` in `frontend/src/features/releases/wizard/store/useWizardStore.ts`
- [X] T039 [US1] Create `PreparePagesStep` that calls `POST /releases/{id}/prepare-pages`, dispatches `initPages` to wizard store, redirects to Settings if `no_release_notes_binding` error, renders one shadcn `<Tabs.Tab>` per prepared page in `frontend/src/features/releases/wizard/steps/PreparePagesStep.tsx`
- [X] T040 [US1] Create `PreparedPageTab` with editable title `<Input>` (shows inline error when >255 chars or empty), markdown body editor (`@uiw/react-md-editor`), and unknown-tokens `<Badge variant="warning">` listing token names; calls `useWizardStore.editPage` on every change in `frontend/src/features/releases/wizard/PreparedPageTab.tsx`

**Checkpoint**: User Story 1 is independently testable — admin can bind templates, configure custom variables, open the wizard, and see auto-filled page content.

---

## Phase 4: User Story 2 — Edit, Reorder, and Cross-Link Prepared Pages (Priority: P1)

**Goal**: Admin can edit page bodies, reorder bindings, publish all pages, and find cross-links on the primary release-notes page in Confluence.

**Independent Test**: Given two bound templates (one with `LinkFromReleaseNotes = true`), the admin edits the checklist body, publishes, then opens Confluence and finds the linked page referenced from the primary release-notes page.

### Backend Implementation

- [X] T041 [US2] Implement `ProjectTemplateBindingService.ReorderAsync` wrapping all sort-order swaps in an explicit `BeginTransactionAsync()` / `CommitAsync()` block, validating that `orderedIds` matches the project's current binding set in `backend/src/RepoManager.Infrastructure/Services/ProjectTemplateBindingService.cs`
- [X] T042 [US2] Add `POST /projects/{projectId}/template-bindings/reorder` endpoint to `ProjectTemplateBindingsController` (Admin role, validates `orderedIds` count matches current bindings, returns updated list) in `backend/src/RepoManager.Api/Controllers/ProjectTemplateBindingsController.cs`
- [X] T043 [US2] Implement `ReleaseRenderService.PublishAsync`: validate all submitted titles (1–255 chars), publish pages via `IConfluencePublisher.CreateOrUpdatePageAsync` in `SortOrder`, collect page IDs, then update the primary `ReleaseNotes` page to append `<ri:page>` cross-links for all pages with `LinkFromReleaseNotes = true` in `backend/src/RepoManager.Infrastructure/Services/ReleaseRenderService.cs`
- [X] T044 [US2] Add `POST /releases/{releaseId}/publish-pages` endpoint to `ReleasesController` mapping to `IReleaseRenderService.PublishAsync`; map `ExternalServiceException` to 502 in `backend/src/RepoManager.Api/Controllers/ReleasesController.cs`

### Frontend Implementation

- [X] T045 [US2] Wire drag-to-reorder into `ProjectPagesTab` using `@dnd-kit/sortable`; call `POST /reorder` on drag-drop end; show optimistic reorder with rollback on error in `frontend/src/features/settings/projects/ProjectPagesTab.tsx`
- [X] T046 [US2] Add `reRenderPages` and `resolveConflict` actions to `useWizardStore`: `reRenderPages` merges fresh pages into existing slots (`server → server`, `edited → conflict`, `conflict → conflict` preserving prior draft); `resolveConflict('keep')` → `edited`; `resolveConflict('discard')` → `server` in `frontend/src/features/releases/wizard/store/useWizardStore.ts`
- [X] T047 [US2] Create `ConflictResolutionDialog` showing per-tab "keep my edits / discard and use fresh render" choice; block the publish button when any slot is in `conflict` state in `frontend/src/features/releases/wizard/ConflictResolutionDialog.tsx`
- [X] T048 [US2] Wire `PreparePagesStep` into the existing `ReleaseWizard` as the first step; add confirmation dialog before `POST /publish-pages`; show per-page publish status list on partial failure (abort-on-first-error with re-run capability) in `frontend/src/features/releases/wizard/`

### Integration Tests

- [X] T049 [US2] Add integration tests for binding CRUD (full create/update/delete), single-`ReleaseNotes` constraint, reorder atomicity, and idempotent re-publish (same release published twice produces zero duplicate pages) in `backend/tests/RepoManager.IntegrationTests/Bindings/TemplateBindingCrudTests.cs` and `backend/tests/RepoManager.IntegrationTests/Releases/PublishPagesTests.cs`

**Checkpoint**: User Stories 1 and 2 are both independently functional — admin can bind, prepare, edit, publish, cross-link, and idempotently re-publish.

---

## Phase 5: User Story 3 — Refresh Prepared Pages with Reconciliation Data (Priority: P2)

**Goal**: After running Jira reconciliation in the wizard, admin clicks "Refresh" and every prepared page is re-rendered with `{{reconciliation}}` context populated; prior manual edits surface as a per-tab keep/discard choice.

**Independent Test**: Given a template with `{{#if reconciliation}}` block, when reconciliation is run then "Refresh pages with reconciliation data" is clicked, the rendered body includes the reconciliation summary panel; without that click, the block is absent.

- [X] T050 [US3] Add `setReconciliationData` and `markReconciliationStale` actions to `useWizardStore`; add `reconciliation: { ran: boolean; stale: boolean; data: ReconciliationSummaryDto | null }` state slice; clear stale flag when new data arrives in `frontend/src/features/releases/wizard/store/useWizardStore.ts`
- [X] T051 [US3] Create `ReconciliationRefreshBar` showing reconciliation status badge (ran / stale / not run), "Refresh pages with reconciliation data" button (disabled when `stale || !ran`); on click calls `POST /prepare-pages` with current `reconciliationData` from store and dispatches `reRenderPages` in `frontend/src/features/releases/wizard/ReconciliationRefreshBar.tsx`
- [X] T052 [US3] Mark reconciliation as stale in `useWizardStore.markReconciliationStale()` when the user changes the release change-range after reconciliation was run; wire `markReconciliationStale` dispatch into the change-range change handler in the wizard in `frontend/src/features/releases/wizard/`
- [X] T053 [P] [US3] Add integration tests for `PreparePagesTests`: prepare without reconciliation (blocks guarded by `{{#if reconciliation}}` are absent), prepare with reconciliation data (blocks render), re-render conflict preservation (edited slot becomes conflict after reRender) in `backend/tests/RepoManager.IntegrationTests/Releases/PreparePagesTests.cs`

**Checkpoint**: User Story 3 is independently functional — reconciliation data flows from wizard state into prepared page bodies without losing manual edits.

---

## Phase 6: User Story 4 — Backward-Compatible Migration (Priority: P2)

**Goal**: Every existing project with `DefaultReleaseNoteTemplateId` set before upgrade produces the same release-notes output after upgrade, with no admin action required.

**Independent Test**: Take a snapshot of a database with three projects each holding a different `DefaultReleaseNoteTemplateId`, run the migration, then run the release wizard for each project and confirm the rendered release-notes title and body match what would have been produced before upgrade.

- [ ] T054 [US4] Audit and verify migration `Up()` edge cases: projects with non-null `DefaultReleaseNoteTemplateId` get exactly one `ReleaseNotes` binding (sort order 0, default title template `{{project.name}} {{version}} — Release Notes`); projects with null get no binding; the column drop runs in the same transaction in `backend/src/RepoManager.Infrastructure/Persistence/Migrations/`
- [ ] T055 [US4] Add integration tests for migration backfill: seed a test database with projects holding `DefaultReleaseNoteTemplateId`, apply migration, assert one `ProjectTemplateBinding` row per project (correct kind, templateId, sortOrder, pageTitleTemplate), assert wizard `PrepareAsync` produces a title matching the pre-upgrade template for each project in `backend/tests/RepoManager.IntegrationTests/Bindings/BindingMigrationTests.cs`

**Checkpoint**: User Story 4 complete — upgrade is safe with zero admin intervention and zero regression in release-notes output.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Capabilities that span multiple user stories or complete the feature's surface area.

- [ ] T056 Extend `PATCH /projects/{projectId}` to accept and persist `versionBumpStrategy` (`Patch | Minor | Major`); add `HasCheckConstraint` validation and update project settings UI to show the bump strategy selector in `backend/src/RepoManager.Api/Controllers/ProjectsController.cs`
- [ ] T057 [P] Implement `ReleaseRenderService.PreviewTemplateAsync` (synthetic sample context or latest-release context for a project) and add `GET /templates/{templateId}/preview?contextSource=synthetic|project&projectId=` endpoint (Viewer role) in `backend/src/RepoManager.Infrastructure/Services/ReleaseRenderService.cs` and `backend/src/RepoManager.Api/Controllers/TemplatesController.cs`
- [ ] T058 [P] Add sample-context selector to the Templates settings preview UI: dropdown with "Synthetic sample" and "Latest release of \<project\>" options; passes `contextSource` and `projectId` to `GET /templates/{id}/preview` in `frontend/src/features/settings/templates/`
- [ ] T059 [P] Add FR-022 admin override version text input in `PreparePagesStep` when server returns `conflict_code: "no_semver_tag"`; re-submits `POST /prepare-pages` with `adminOverrideVersion` in `frontend/src/features/releases/wizard/steps/PreparePagesStep.tsx`
- [ ] T060 [P] Add FR-018 wizard guard: if project has zero bindings or no `ReleaseNotes` binding, refuse to open the wizard and redirect to Settings → Projects → Pages with explanatory banner in `frontend/src/features/releases/wizard/`
- [ ] T061 Run the quickstart.md smoke test end-to-end: bind two templates, add custom variable `slackChannel`, open wizard, verify two tabs with correct auto-filled titles, edit checklist body, navigate away and back (edits persist), publish, verify two cross-linked Confluence pages, re-publish (zero duplicates), add typo variable to template and verify unknown-token warning badge appears

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — **BLOCKS** all user stories
- **US1 (Phase 3)**: Depends on Phase 2 — can start immediately after Foundational
- **US2 (Phase 4)**: Depends on Phase 3 completion (ReorderAsync and PublishAsync extend US1 services)
- **US3 (Phase 5)**: Depends on Phase 3 (wizard store) and Phase 4 (reRenderPages DraftState conflict merge)
- **US4 (Phase 6)**: Depends on Phase 2 (migration) — can run independently of US1/US2/US3
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational — no dependency on other stories
- **US2 (P1)**: Depends on US1 (extends binding service + wizard store + controllers)
- **US3 (P2)**: Depends on US1 (wizard store) and US2 (DraftState conflict merge)
- **US4 (P2)**: Depends on Phase 2 migration only — can parallel with US1/US2/US3

### Within Each User Story

- TDD: tests MUST be written and RED before any service implementation (US1 only mandates this; other stories add integration tests after implementation)
- Domain entities → Application DTOs/interfaces → EF config → Implementation → Controllers → Frontend

### Parallel Opportunities

- T001–T004 (domain entities + enums): all parallelizable
- T006–T008 (service interfaces): all parallelizable
- T009–T012 (DTO groups): all parallelizable
- T013–T014 (validators): parallelizable
- T015–T016 (EF configs): parallelizable
- T019–T020 (MissingTokenRecorder + HandlebarsHelpers): parallelizable
- T022–T024 (failing unit tests): all parallelizable
- T027, T028–T029, T030 within US1 backend: some parallelizable (different controllers, different services)
- T032–T036 within US1 frontend: several parallelizable (different files)
- US4 (T054–T055) can run in parallel with US3 after Phase 2

---

## Parallel Example: User Story 1

```bash
# Write all failing unit tests together (TDD gate):
T022: ReleaseRenderServiceTests.cs
T023: HandlebarsHelpersTests.cs
T024: ProjectTemplateBindingServiceTests.cs

# Frontend settings components (after T031 API client regenerated):
T034: ProjectPagesTab.tsx
T035: BindingFormSheet.tsx
T036: CustomVariablesSection.tsx
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (domain entities, enums)
2. Complete Phase 2: Foundational (interfaces, DTOs, validators, EF config, migration, Handlebars)
3. Complete Phase 3: User Story 1 (TDD tests → services → controllers → frontend settings + wizard)
4. **STOP and VALIDATE**: Two published Confluence pages with auto-filled values
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → All contracts and schema in place
2. User Story 1 → Test publish two auto-filled pages → **Demo**
3. User Story 2 → Test edit, reorder, cross-link → **Demo**
4. User Story 3 → Test reconciliation refresh → **Demo**
5. User Story 4 → Test upgrade safety → **Release**
6. Polish → FR-022 override, FR-024 template preview, smoke test

### Parallel Team Strategy

With multiple developers, once Foundational is complete:
- **Developer A**: User Story 1 backend (T022–T030)
- **Developer B**: User Story 1 frontend (T031–T040)
- **Developer C**: User Story 4 migration tests (T054–T055) ← independent of US1/US2/US3

---

## Notes

- [P] tasks touch different files and have no intra-phase dependencies
- [USn] label maps each task to its user story for traceability
- TDD tests (T022–T024) MUST fail RED before any service implementation starts
- Apply migration against both a fresh DB and an existing DB with data before moving to US1 implementation
- Commit after each completed phase or logical group
- Stop at each checkpoint to validate the story independently before moving to the next
- `PublishAsync` must never be called from a request handler — it is invoked by explicit user action only
- Server is stateless for wizard sessions — all edits live in the Zustand store, never persisted server-side
