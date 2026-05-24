# Quickstart: Project Page Templates

**Branch**: `007-project-page-templates`
**Date**: 2026-05-24

This guide gives the implementation sequence and smoke test for feature 007. Follow the numbered steps in order; each step is a self-contained vertical slice that can be compiled and tested independently.

---

## Prerequisites

- Feature 005 (Per-Repo Release Versioning) implemented: `ReleaseRepository` entity and `IVersionBumpService` are present.
- Milestone 7 (Handlebars templates) and Milestone 8 (Confluence publishing) complete: `ReleaseNoteTemplate` entity and `IConfluencePublisher` are present.
- `dotnet ef` CLI installed and `backend/` source tree exists.

---

## Backend Sequence

### Step 1 — Domain entities

Add to `RepoManager.Domain/Entities/`:
- `ProjectTemplateBinding.cs` — POCO with all fields from `data-model.md`.
- `ProjectCustomVariable.cs` — POCO.

Add to `RepoManager.Domain/Enums/`:
- `TemplateBindingKind.cs` — `ReleaseNotes | Checklist | Custom`.
- `VersionBumpStrategy.cs` — `Patch | Minor | Major`.

Modify `Project.cs`:
- Add `VersionBumpStrategy VersionBumpStrategy { get; set; }` with default `Minor`.
- Remove `DefaultReleaseNoteTemplateId` property (will be migrated away).

Add navigation properties:
- `Project.TemplateBindings` → `ICollection<ProjectTemplateBinding>`.
- `Project.CustomVariables` → `ICollection<ProjectCustomVariable>`.

**Gate**: `dotnet build RepoManager.Domain` passes.

---

### Step 2 — TDD: Write failing unit tests first

In `RepoManager.UnitTests/`, create **before any service implementation**:

`ReleaseRenderServiceTests.cs`:
- `RenderContext_WithUnknownToken_RendersEmptyString` — verifies `{{custom.typo}}` renders as `""`.
- `RenderContext_WithUnknownToken_CapturesTokenName` — verifies `unknownTokens` list contains `"custom.typo"`.
- `PreparePages_WhenNoSemverTag_ThrowsValidationException` — verifies `no_semver_tag` error.
- `PreparePages_WhenNoReleaseNotesBinding_ThrowsValidationException` — verifies `no_release_notes_binding` error.
- `BuildContext_PrimaryRepoHasReleaseRepositoryRow_UsesSnapshotVersion` — verifies Feature 005 snapshot is preferred.
- `BuildContext_NoPrimaryRepoSnapshot_AppliesBumpStrategy` — verifies fallback uses `VersionBumpStrategy`.

`HandlebarsHelpersTests.cs`:
- One test per helper: `formatDate`, `length`, `eq`, `gt`, `minus`, `lower`, `upper`, `truncate`, `jiraLink`.

`ProjectTemplateBindingServiceTests.cs`:
- `Delete_LastReleaseNotesBinding_ThrowsConflictException`.
- `Create_SecondReleaseNotesBinding_ThrowsConflictException`.

Run: `dotnet test --filter Category=Unit` — all new tests fail red.

---

### Step 3 — Application layer

In `RepoManager.Application/`:

**Services** (interfaces only — no implementation yet):
- `IProjectTemplateBindingService.cs`
- `IProjectCustomVariableService.cs`
- `IReleaseRenderService.cs`

**DTOs** (from `contracts/service-interfaces.md`):
- `DTOs/Bindings/ProjectTemplateBindingDto.cs`
- `DTOs/Bindings/CreateBindingRequest.cs`
- `DTOs/Bindings/UpdateBindingRequest.cs`
- `DTOs/CustomVariables/ProjectCustomVariableDto.cs`
- `DTOs/Releases/ReleaseRenderContextDto.cs`, `RepoContextDto.cs`, etc.
- `DTOs/Releases/PreparedPageDto.cs`, `PreparedReleaseDto.cs`
- `DTOs/Releases/PreparePageRequest.cs`, `PublishPagesRequest.cs`, `PublishResultDto.cs`

**Validators**:
- `CreateBindingRequestValidator.cs`
- `UpdateBindingRequestValidator.cs`
- `PublishPagesRequestValidator.cs`
- `ProjectCustomVariableUpsertValidator.cs`

---

### Step 4 — EF Core configuration and migration

In `RepoManager.Infrastructure/Persistence/`:
- `EntityConfigurations/ProjectTemplateBindingConfiguration.cs` — all constraints, indexes, FK behavior.
- `EntityConfigurations/ProjectCustomVariableConfiguration.cs` — unique index on `(ProjectId, Key)`.
- Update `AppDbContext.cs`: add `DbSet<ProjectTemplateBinding>`, `DbSet<ProjectCustomVariable>`.

Generate migration:
```powershell
dotnet ef migrations add AddProjectTemplateBindings `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api
```

Verify the migration file, then hand-edit `Up()` to add:
1. The data backfill SQL (see `data-model.md` Migration section).
2. The `DROP COLUMN DefaultReleaseNoteTemplateId` at the end.

Apply:
```powershell
dotnet ef database update `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api
```

**Gate**: Migration applies cleanly on a fresh database and on a database with existing projects (backfill rows appear correctly).

---

### Step 5 — MissingTokenRecorder and Handlebars setup

In `RepoManager.Infrastructure/Services/Handlebars/`:
- `MissingTokenRecorder.cs` — implements `IFormatterProvider` with `[ThreadStatic]` capture bag (see `research.md` Decision 1 for the implementation pattern).
- `HandlebarsHelpers.cs` — static class registering all 9 helpers.
- `HandlebarsFactory.cs` — creates and caches the `IHandlebars` instance with `MissingTokenRecorder` and all helpers registered.

Register in DI:
```csharp
services.AddSingleton<MissingTokenRecorder>();
services.AddSingleton<IHandlebars>(sp =>
    HandlebarsFactory.Create(sp.GetRequiredService<MissingTokenRecorder>()));
```

**Gate**: `dotnet test --filter HandlebarsHelpers` passes green.

---

### Step 6 — IReleaseRenderService implementation

In `RepoManager.Infrastructure/Services/ReleaseRenderService.cs`:

`BuildContextAsync(releaseId, overrideVersion)`:
1. Load `Release` + project + template bindings + custom variables.
2. Validate `ReleaseNotes` binding exists.
3. Resolve `version` / `previousVersion`: prefer `ReleaseRepository.NextVersion` for primary repo; fall back to tag + `VersionBumpStrategy`.
4. Load `repositories`, `tickets`, `contributors` from Release snapshot (Feature 005).
5. Construct `ReleaseRenderContextDto`.

`PrepareAsync(releaseId, request)`:
1. Call `BuildContextAsync`.
2. For each binding (ordered by `SortOrder`): call `MissingTokenRecorder.BeginCapture()`, render title, render body, call `EndCapture()`.
3. Validate each title (length 1–255); if violated, still return the page but add a `warnings` entry (title validation blocks publish, not prepare).
4. Detect title collisions across pages; add `warnings`.
5. Return `PreparedReleaseDto`.

`PublishAsync(releaseId, request)`:
1. Validate all submitted titles (1–255 chars) — throw `ValidationException` with per-page errors if any fail.
2. Publish pages in `SortOrder` via `IConfluencePublisher.CreateOrUpdatePageAsync`.
3. Collect `(bindingId → confluencePageId)` map.
4. After all pages published: find the `ReleaseNotes` page; append a "Related pages" section with `<ri:page>` links for all pages with `LinkFromReleaseNotes = true`; call `UpdatePageAsync`.
5. Return `PublishResultDto`.

**Gate**: Unit tests from Step 2 pass green.

---

### Step 7 — IProjectTemplateBindingService and IProjectCustomVariableService

In `RepoManager.Infrastructure/Services/`:
- `ProjectTemplateBindingService.cs` — implements all 5 methods; enforce single-`ReleaseNotes` constraint; wrap reorder in a transaction.
- `ProjectCustomVariableService.cs` — implements get/upsert/delete; upsert uses `ExecuteUpdate`/`ExecuteInsert` pattern under the unique index.

Add audit log calls in both services (FR-019): `_auditLog.RecordAsync(actor, "binding.create", ...)`.

**Gate**: `dotnet test --filter ProjectTemplateBindingService` passes green.

---

### Step 8 — Controllers and DI wiring

In `RepoManager.Api/Controllers/`:
- `ProjectTemplateBindingsController.cs` — 5 endpoints.
- `ProjectCustomVariablesController.cs` — 3 endpoints.

Update `ReleasesController.cs`:
- `POST /{id}/prepare-pages` → `IReleaseRenderService.PrepareAsync`.
- `POST /{id}/publish-pages` → `IReleaseRenderService.PublishAsync`.

Update `TemplatesController.cs` (or create if not present):
- `GET /{id}/preview` with `contextSource` query param → `IReleaseRenderService.PreviewTemplateAsync`.

Register services in DI (`Program.cs`):
```csharp
services.AddScoped<IProjectTemplateBindingService, ProjectTemplateBindingService>();
services.AddScoped<IProjectCustomVariableService, ProjectCustomVariableService>();
services.AddScoped<IReleaseRenderService, ReleaseRenderService>();
```

Update `PATCH /projects/{id}` handler to accept and persist `versionBumpStrategy`.

**Gate**: `dotnet build backend/src` clean; `dotnet run` starts with no errors; Swagger UI shows all new endpoints.

---

### Step 9 — Integration tests

In `RepoManager.IntegrationTests/`:
- `Bindings/TemplateBindingCrudTests.cs` — full CRUD flow; single-`ReleaseNotes` constraint.
- `Bindings/BindingMigrationTests.cs` — snapshot of DB with `DefaultReleaseNoteTemplateId`, run migration, verify bindings.
- `Releases/PreparePagesTests.cs` — prepare with and without reconciliation; unknown token warning; title collision warning.
- `Releases/PublishPagesTests.cs` — publish two pages; cross-link update; idempotent re-publish.

**Gate**: All integration tests pass against a real SQLite test database.

---

## Frontend Sequence

### Step 10 — Regenerate OpenAPI client

After the backend is running:
```powershell
npm run generate-api  # from frontend/
```

Verify new types appear: `ProjectTemplateBindingDto`, `PreparedReleaseDto`, `PublishResultDto`, etc.

---

### Step 11 — Settings: Project Pages tab

In `frontend/src/features/settings/projects/`:
- `ProjectPagesTab.tsx` — list bindings with shadcn `<Table>`; drag-to-reorder via `@dnd-kit/sortable`; add/edit/delete binding via sheet/dialog.
- `BindingFormSheet.tsx` — form using React Hook Form + Zod; selects available templates; all fields from `CreateBindingRequest`.
- `CustomVariablesSection.tsx` — key/value list with inline add/edit/delete.
- Hook: `useProjectBindings(projectId)` via TanStack Query.

UX requirements:
- Deleting the last `ReleaseNotes` binding: show a `<Alert variant="destructive">` explaining that at least one binding is required.
- Reorder: drag handles; POST to `/reorder` on drop.
- Loading skeleton while fetching; toast on save/delete success and error.

---

### Step 12 — Wizard: Prepare Pages step

In `frontend/src/features/releases/wizard/steps/`:
- `PreparePagesStep.tsx` — calls `POST /releases/{id}/prepare-pages`; stores result in `useWizardStore.initPages`; renders one shadcn `<Tabs.Tab>` per page.
- `PreparedPageTab.tsx` — editable title input + markdown body editor (`@uiw/react-md-editor`); calls `useWizardStore.editPage` on change; shows `UnknownTokens` warning badge if non-empty.
- `ReconciliationRefreshBar.tsx` — shows reconciliation status; "Refresh pages with reconciliation data" button (disabled when `stale || !ran`); on click, calls `POST /prepare-pages` with reconciliation data and dispatches `useWizardStore.reRenderPages`.
- `ConflictResolutionDialog.tsx` — shown when any slot is in `conflict` state before publish; per-tab "keep / discard" choice.
- Wire the Prepare Pages step into the existing `ReleaseWizard.tsx` as the first step.

UX requirements (per spec):
- Title validation error shown inline on the tab — publish button blocked.
- Title collision warning shown as a non-blocking `<Alert>` in the preview step.
- Confirmation dialog before `POST /publish-pages` ("This will create or update Confluence pages for the release. Proceed?").
- Loading state while preparing and while publishing.

Store integration:
- `useWizardStore` (from `contracts/service-interfaces.md`) persisted to `sessionStorage`.
- Wizard reset (`resetWizard()`) on wizard close/cancel.

---

## Smoke Test

**Preconditions**:
- Two `ReleaseNoteTemplate` rows exist (e.g., "Standard Release Notes", "Smoke Checklist").
- Project "Payments" has a version-primary repository on tag `1.30.0`.
- Confluence space configured.

**Sequence**:
1. Admin opens Settings → Projects → Payments → Pages.
2. Adds binding: template "Standard Release Notes", kind `ReleaseNotes`, title `{{project.name}} {{version}} — Release Notes`, sort order 0.
3. Adds binding: template "Smoke Checklist", kind `Checklist`, title `{{project.name}} {{version}} — Smoke Tests`, `LinkFromReleaseNotes = true`, sort order 1.
4. Adds custom variable `slackChannel = #payments-releases`.
5. Opens release wizard for Payments.
6. Wizard opens "Prepare pages" step — two tabs appear: "Payments 1.31.0 — Release Notes", "Payments 1.31.0 — Smoke Tests".
7. Admin edits the checklist body; navigates to "Preview" step and back — edits persist.
8. Admin publishes — two Confluence pages created; "Release Notes" page contains a link to "Smoke Tests".
9. Admin re-publishes — zero duplicate pages; links intact.
10. Admin sets custom variable `slakChannel` (typo) in a template — warning badge appears on the tab, render still succeeds.

**Pass criteria**: All 10 steps complete without errors matching FR-001 through FR-024.
