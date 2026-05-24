# Tasks: Project Page Templates

**Input**: Design documents from `/specs/012-project-page-templates/`
**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `contracts/`

**Tests**: Test tasks are included because the project's constitution requires unit tests for domain logic, integration tests for cross-layer flows, and snapshot tests for template rendering. They are marked with `[Test]` for clarity.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing. `[P]` marks tasks that can run in parallel (different files, no dependencies). `[Story]` indicates which user story the task supports.

**Path conventions**: Web application layout per `plan.md`. Backend: `backend/src/RepoManager.*` and `backend/tests/*`. Frontend: `frontend/src/*` and `frontend/tests/*`.

---

## Phase 1 — Setup

- [ ] **T001** Create folder scaffolding under `backend/src/RepoManager.Application/Releases/Rendering/` and `backend/src/RepoManager.Application/ProjectTemplateBindings/`; add empty `README.md` per folder describing purpose.
- [ ] **T002** [P] Create folder scaffolding under `frontend/src/pages/settings/projects/[id]/pages/` and `frontend/src/pages/releases/wizard/`.
- [ ] **T003** [P] Add `@dnd-kit/core` and `@dnd-kit/sortable` to `frontend/package.json`; commit lockfile update.

## Phase 2 — Foundational (no story can start until this phase completes)

> Domain entities, enums, EF configuration, migrations, and seed migration. Every story depends on these being in place.

- [ ] **T004** Add enum `TemplateKind` (`ReleaseNotes`, `Checklist`, `Custom`) at `backend/src/RepoManager.Domain/Templates/TemplateKind.cs`.
- [ ] **T005** Add enum `VersionBumpStrategy` (`Patch`, `Minor`, `Major`) at `backend/src/RepoManager.Domain/Projects/VersionBumpStrategy.cs`.
- [ ] **T006** Add entity `ProjectTemplateBinding` at `backend/src/RepoManager.Domain/Templates/ProjectTemplateBinding.cs` with fields per `plan.md` data-model section; include domain invariant constructor enforcing non-empty `PageTitleTemplate`.
- [ ] **T007** Add value object `ProjectCustomVariable(string Key, string Value)` at `backend/src/RepoManager.Domain/Projects/ProjectCustomVariable.cs`.
- [ ] **T008** Extend `Project` entity at `backend/src/RepoManager.Domain/Projects/Project.cs` with `VersionBumpStrategy` (default `Minor`) and `IReadOnlyCollection<ProjectCustomVariable> CustomVariables`; mark `DefaultReleaseNoteTemplateId` as `[Obsolete]`.
- [ ] **T009** Add EF configuration `ProjectTemplateBindingConfiguration` at `backend/src/RepoManager.Infrastructure/Persistence/EfConfigurations/ProjectTemplateBindingConfiguration.cs` with unique index `(ProjectId, TemplateId, Kind)` and index `(ProjectId, SortOrder)`.
- [ ] **T010** Extend `ProjectConfiguration` at `backend/src/RepoManager.Infrastructure/Persistence/EfConfigurations/ProjectConfiguration.cs` to map `VersionBumpStrategy` and to serialise `CustomVariables` to a JSON column `CustomVariablesJson`.
- [ ] **T011** Add EF migration `20260524_AddProjectTemplateBindings` creating the `ProjectTemplateBindings` table.
- [ ] **T012** Add EF migration `20260524_AddProjectVersionBumpStrategy` adding the enum column to `Projects` with default `1` (Minor).
- [ ] **T013** Add EF migration `20260524_AddProjectCustomVariables` adding the nullable `CustomVariablesJson` column to `Projects`.
- [ ] **T014** Add data migration `20260524_SeedDefaultReleaseNotesBindings` inserting one `ProjectTemplateBinding` of kind `ReleaseNotes` per project with `DefaultReleaseNoteTemplateId IS NOT NULL`, using default page-title template `"{{project.name}} {{version}} — Release Notes"` and sort order 0.
- [ ] **T015** [Test] Add migration test `BindingMigrationTests` at `backend/tests/integration/BindingMigrationTests.cs` seeding two projects (one with default template, one without) and asserting exactly the expected bindings after migration. Run against an in-memory SQLite database.

**Checkpoint**: Foundational phase complete. Database accepts the new schema; existing projects auto-migrate; no user-facing behaviour has changed yet.

---

## Phase 3 — User Story 1: Bind multiple auto-filled page templates to a project (P1)

**Goal**: Admins can bind templates to a project and the wizard produces multiple auto-filled pages.

**Independent Test**: Bind two templates, run wizard, get two published Confluence pages with auto-filled values.

### Application layer

- [ ] **T016** [P] [Story:US1] Create record `ReleaseRenderContext` at `backend/src/RepoManager.Application/Releases/Rendering/ReleaseRenderContext.cs` with nested records `ProjectContext`, `RepoContext`, `TicketBuckets`, `TicketContext`, `ContributorContext`, `ReconciliationSummary`, `ConfluenceContext` per `spec.md` Key Entities.
- [ ] **T017** [P] [Story:US1] Create interface `IVersionResolver` and implementation `VersionResolver` at `backend/src/RepoManager.Application/Releases/VersionResolver.cs`. Resolves `(PreviousTag, NextTag, JiraFixVersionName)` per repository given the project's `VersionBumpStrategy` and the existing `<RepoName>_<NextVersion>` Jira convention.
- [ ] **T018** [Test] [Story:US1] Add `VersionResolverTests` at `backend/tests/unit/VersionResolverTests.cs` covering Patch/Minor/Major strategies, tagless repos (returns error result), and the Jira fix-version naming.
- [ ] **T019** [Story:US1] Create `RenderContextBuilder` at `backend/src/RepoManager.Application/Releases/Rendering/RenderContextBuilder.cs` that, given a project and the existing `IReleaseChangeService`, `IGitProvider`, `IJiraService` (read-only), builds a `ReleaseRenderContext`.
- [ ] **T020** [Story:US1] Create `PageRenderer` at `backend/src/RepoManager.Application/Releases/Rendering/PageRenderer.cs` that wraps `Handlebars.Net`, owns helper registration, and exposes `RenderTitle(template, context)` and `RenderBody(template, context)`.
- [ ] **T021** [Story:US1] Extend `HandlebarsHelpers` at `backend/src/RepoManager.Application/Templates/HandlebarsHelpers.cs` adding `gt`, `minus`, `lower`, `upper`, `truncate`, `jiraLink` helpers; ensure registration happens once at app startup.
- [ ] **T022** [Story:US1] Implement missing-member resolver `RecordingMissingMemberResolver` to capture unknown token references during render; expose collected names on `PageRenderer.LastRenderWarnings`.
- [ ] **T023** [Story:US1] Create interface `IReleasePagePreparationService` and implementation `ReleasePagePreparationService` at `backend/src/RepoManager.Application/Releases/ReleasePagePreparationService.cs`. Orchestrates: load project + bindings, build context, render each binding into a `PreparedPage`, return `PreparedRelease`.
- [ ] **T024** [Test] [Story:US1] Add `ReleasePagePreparationServiceTests` at `backend/tests/unit/ReleasePagePreparationServiceTests.cs` with fake `IGitProvider` and `IJiraService` covering: zero bindings → error, one ReleaseNotes binding → one page, two bindings → two pages in `SortOrder`, unknown token → empty rendered + warning surfaced.
- [ ] **T025** [Test] [P] [Story:US1] Add snapshot tests at `backend/tests/snapshot/HandlebarsRenderingSnapshots/` for the sample release-notes template against a fixed `ReleaseRenderContext` fixture so template-engine upgrades don't silently change output.

### Binding CRUD service

- [ ] **T026** [Story:US1] Create `ProjectTemplateBindingService` at `backend/src/RepoManager.Application/ProjectTemplateBindings/ProjectTemplateBindingService.cs` with `ListAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `ReorderAsync`; enforces "exactly one `ReleaseNotes` binding per project".
- [ ] **T027** [P] [Story:US1] Add FluentValidation validators for `CreateBindingDto`, `UpdateBindingDto` enforcing non-empty `PageTitleTemplate` ≤ 500 chars and custom-variable key pattern `[a-zA-Z][a-zA-Z0-9_]*`.
- [ ] **T028** [Test] [Story:US1] Add `ProjectTemplateBindingValidationTests` at `backend/tests/unit/ProjectTemplateBindingValidationTests.cs` covering validator rules and the "last ReleaseNotes binding cannot be deleted" rule.

### API layer

- [ ] **T029** [Story:US1] Create `ProjectTemplateBindingsController` at `backend/src/RepoManager.Api/Controllers/ProjectTemplateBindingsController.cs` with the six endpoints listed in `plan.md` Phase 1 contracts; Admin authorization on writes, Viewer-or-Admin on reads.
- [ ] **T030** [Story:US1] Extend `ReleasesController` at `backend/src/RepoManager.Api/Controllers/ReleasesController.cs` adding `POST /api/v1/projects/{id}/releases/preview` returning `PreparedReleaseDto`. Idempotent; side-effect-free.
- [ ] **T031** [Story:US1] Extend `Projects` settings endpoints to read and write `VersionBumpStrategy` and `CustomVariables`.

### Frontend — Settings → Projects → Pages

- [ ] **T032** [Story:US1] Build API client functions at `frontend/src/api/templateBindings.ts`: `getBindings`, `createBinding`, `updateBinding`, `deleteBinding`, `reorderBindings`.
- [ ] **T033** [P] [Story:US1] Build `PagesTab` page at `frontend/src/pages/settings/projects/[id]/pages/PagesTab.tsx` hosting bindings table and custom-variables card.
- [ ] **T034** [Story:US1] Build `BindingsTable` component with drag-handle reorder (via `@dnd-kit/sortable`) at `frontend/src/pages/settings/projects/[id]/pages/BindingsTable.tsx`.
- [ ] **T035** [Story:US1] Build `BindingDialog` modal at `frontend/src/pages/settings/projects/[id]/pages/BindingDialog.tsx` with template combobox, kind select, title-template input with token autocomplete (suggesting `project.name`, `version`, etc.), link-flag switch, parent-page override input.
- [ ] **T036** [P] [Story:US1] Build `CustomVariablesCard` at `frontend/src/pages/settings/projects/[id]/pages/CustomVariablesCard.tsx` — editable key/value table.

### Frontend — Wizard Prepare Pages step

- [ ] **T037** [Story:US1] Extend `wizardState.ts` to hold a `pages[]` array instead of a single `notes` string; preserve in-flight edits across step navigation.
- [ ] **T038** [Story:US1] Add `previewRelease` to `frontend/src/api/releases.ts` and update the existing `createRelease` to accept a `pages[]` body.
- [ ] **T039** [Story:US1] Build `PreparePagesStep` at `frontend/src/pages/releases/wizard/PreparePagesStep.tsx` — calls `previewRelease`, shows resolved context summary (version, previous version, per-repo table) and one tab per prepared page.
- [ ] **T040** [Story:US1] Update wizard step machine to use `PreparePagesStep` as step 1, replacing the old "Choose template" step.

**Checkpoint US1**: A tech lead can bind two templates to a project, run the wizard, see two prepared pages, and publish to Confluence with auto-filled values. End-to-end flow works without US2, US3, or US4.

---

## Phase 4 — User Story 2: Edit, reorder, and cross-link prepared pages (P1)

**Goal**: Admins can edit any prepared page in-wizard and have cross-linking happen on publish.

**Independent Test**: Edit one prepared page's body, publish with `LinkFromReleaseNotes` on a sibling, verify the release-notes page in Confluence contains the related-pages link.

- [ ] **T041** [Story:US2] Extend `ReleasePagePreparationService` to mark a `PreparedPage` as `IsEdited` when its body or title differs from the freshly rendered value; tracked client-side, sent back on publish.
- [ ] **T042** [Story:US2] Extend `ConfluencePublisher` at `backend/src/RepoManager.Infrastructure/Confluence/ConfluencePublisher.cs` adding `AppendRelatedPagesSection(primaryPageId, related: List<(string title, string url)>)`. Use a marker comment block (`<!-- related-pages:start -->` / `<!-- related-pages:end -->`) so re-publishes idempotently replace the section instead of duplicating it.
- [ ] **T043** [Story:US2] Extend the publish orchestration in `ReleasesController` (or its underlying service) to publish pages in `SortOrder`, collect URLs, and call `AppendRelatedPagesSection` on the primary page with every sibling whose binding has `LinkFromReleaseNotes = true`.
- [ ] **T044** [Story:US2] Add pre-publish validation: every prepared page title is non-empty and ≤ 255 chars; surface duplicate-title warnings within the same parent.
- [ ] **T045** [Story:US2] Build `EditPagesStep` (multi-tab) at `frontend/src/pages/releases/wizard/EditPagesStep.tsx`, replacing the single-textarea editor with one Monaco editor per tab.
- [ ] **T046** [P] [Story:US2] Build `PreviewStep` (multi-tab) at `frontend/src/pages/releases/wizard/PreviewStep.tsx` showing the Confluence-rendered preview for each prepared page.
- [ ] **T047** [Story:US2] Show a confirmation dialog in the Pages settings UI when admin attempts to delete the last `ReleaseNotes` binding (server already rejects; UI prevents the request).
- [ ] **T048** [Test] [Story:US2] Add integration test `PreviewAndPublishFlowTests.PublishesMultiplePagesInOrderWithCrossLinks` at `backend/tests/integration/PreviewAndPublishFlowTests.cs`.
- [ ] **T049** [Test] [P] [Story:US2] Add E2E test `projectPagesFlow.spec.ts` at `frontend/tests/e2e/projectPagesFlow.spec.ts` (Playwright) walking the full happy path of US1 + US2 against a mocked backend.

**Checkpoint US2**: Multi-page editing, ordering, and cross-linking work end-to-end.

---

## Phase 5 — User Story 3: Refresh pages with Jira reconciliation data (P2)

**Goal**: Reconciliation result flows into the rendered pages, with manual edits preserved as draft.

**Independent Test**: Run reconciliation, click "Refresh pages with reconciliation data", confirm the reconciliation summary block appears in the rendered output and a per-page edit-conflict prompt appears for any tab the admin had edited.

- [ ] **T050** [Story:US3] Extend `ReleaseRenderContext` already has the `Reconciliation` slot; wire `ReleasePagePreparationService` to accept an optional `ReconciliationSummary` argument and pass it through.
- [ ] **T051** [Story:US3] Add `POST /api/v1/projects/{id}/releases/preview/refresh` accepting the current `ReconciliationSummary` and returning a fresh `PreparedReleaseDto`.
- [ ] **T052** [Story:US3] Implement reconciliation staleness hash: hash `(repoId, previousTag, headSha)` tuples at reconciliation time; expose `IsStale` on the wizard's reconciliation summary view.
- [ ] **T053** [Story:US3] Frontend: surface a "Refresh pages with reconciliation data" button on the Reconcile step; disable when stale.
- [ ] **T054** [Story:US3] Frontend: when a refresh would overwrite an edited tab, show a per-tab prompt "Keep my edits" or "Discard and use freshly rendered output".
- [ ] **T055** [Test] [Story:US3] Add unit test for staleness hashing; integration test for the refresh endpoint.

**Checkpoint US3**: Reconciliation data flows into templates with a clean conflict-resolution UX.

---

## Phase 6 — User Story 4: Backward-compatible migration (P2)

**Goal**: Existing projects upgrade silently and continue to publish equivalent release notes.

**Independent Test**: Snapshot a pre-upgrade database, run the migration, run the wizard for each project, compare the rendered output to a pre-upgrade fixture.

- [ ] **T056** [Story:US4] Add an `[Obsolete]` deprecation warning on `Project.DefaultReleaseNoteTemplateId` getter that fires when read outside the seed migration and the Settings UI fallback code path.
- [ ] **T057** [Story:US4] Add a one-time "Setup required" banner on the project page when the project has zero bindings (post-upgrade for projects that never had a default template).
- [ ] **T058** [Story:US4] Block `POST /api/v1/projects/{id}/releases/preview` with HTTP 409 when the project has no `ReleaseNotes` binding; frontend redirects to the Pages settings tab.
- [ ] **T059** [Test] [Story:US4] Extend `BindingMigrationTests` (from T015) with a fixture-based equivalence check: render the project's pre-upgrade release notes with the old code path and the post-migration code path; assert byte-equivalent output for the title and structural-equivalent for the body.

**Checkpoint US4**: Upgrade is safe; no project loses release-publishing capability.

---

## Phase 7 — Polish

- [ ] **T060** [P] Add a "Sample context source" dropdown to the existing Templates preview pane at `frontend/src/pages/settings/templates/TemplatePreviewPane.tsx`: options are "Synthetic sample" (default) and "Latest release of <project>" for every project. Loads cached real context for accurate preview.
- [ ] **T061** [P] Documentation: update `README.md` and `/docs/releases.md` with the new wizard flow, the Pages settings tab, and the custom-variables non-secret warning.
- [ ] **T062** [P] Telemetry: add log events `binding.created|updated|deleted|reordered`, `release.preview.rendered`, `release.publish.completed`, each with correlation IDs and timings.
- [ ] **T063** Run the full smoke test from `spec.md` US1 against a staging environment; capture screenshots; attach to release.
- [ ] **T064** Verify performance target SC-002 against a project with 3 repos, 30 commits, 15 tickets; record p50 and p95 of `/releases/preview`.

---

## Dependencies

- **Phase 1 (Setup)** → no prereqs.
- **Phase 2 (Foundational)** → Phase 1 complete.
- **User Story 1 (Phase 3)** → Phase 2 complete; independently shippable.
- **User Story 2 (Phase 4)** → Phase 2 complete; depends on US1 for the wizard step structure but can be developed in parallel with later parts of US1 (specifically T029–T040) once the binding entity and `PreparedPage` exist.
- **User Story 3 (Phase 5)** → US1 complete (renderer + wizard step exist); independent of US2.
- **User Story 4 (Phase 6)** → Phase 2 seed migration (T014) plus US1 service surface; mostly a deployment-safety phase.
- **Polish (Phase 7)** → all user stories complete.

## Parallel Execution Hints

- After Phase 2 finishes, T016, T017, T019, T020, T021, T022 (Application-layer rendering pieces) all touch different files and can be split across developers.
- Frontend tasks T033, T034, T035, T036 share a folder but different files; can be developed in parallel.
- Tests marked `[P]` (T015, T018, T025, T028, T049, T055, T059) can run alongside their implementation peers.

## Suggested Implementation Order

1. Phase 1 + Phase 2 (single PR; foundation must be solid).
2. Phase 3 (US1) shipped behind a feature flag if your team uses one — this is the riskiest user-facing change.
3. Phase 6 (US4) verified in staging immediately after Phase 3; gates the production rollout.
4. Phase 4 (US2) next — cross-linking and multi-tab editing.
5. Phase 5 (US3) last among user stories — depends on the rest.
6. Phase 7 (Polish) before marking the feature complete.
