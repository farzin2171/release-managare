# Tasks: Milestone 13 — Security, Service Ownership & UX Hardening

**Input**: Design documents from `specs/008-milestone-13-hardening/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, data-model.md ✓, contracts/ ✓, quickstart.md ✓

**Tests**: TDD is required for `SetupKeyAuthorizationFilter` and `SetupKeyStartupValidator` per the constitution (unit tests written and confirmed failing before implementation). Integration tests are included for all new backend behaviour. Frontend tests included for the session-renewal E2E.

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no shared dependencies)
- **[US#]**: Which user story this task belongs to
- Exact file paths from `specs/008-milestone-13-hardening/plan.md`

---

## Phase 1: Setup & TDD Gate

**Purpose**: Write failing unit tests for the two TDD-required components before any implementation begins. Confirm they are red. This gate must pass before Phase 3 begins.

- [X] T001 Write failing unit tests for `SetupKeyAuthorizationFilter` in `backend/tests/RepoManager.UnitTests/Filters/SetupKeyAuthorizationFilterTests.cs` — covers: missing header → 401; wrong key → 401; correct key → next() called; constant-time comparison used
- [X] T002 Write failing unit tests for `SetupKeyStartupValidator` in `backend/tests/RepoManager.UnitTests/StartupValidators/SetupKeyStartupValidatorTests.cs` — covers: key absent + no users → StopApplication called + Fatal logged; key absent + users exist → no stop; key present (any) → no stop
- [X] T003 [P] Confirm test runner sees the new test files and they fail: `dotnet test backend/tests --filter "FullyQualifiedName~SetupKey"` — expect all new tests red
- [X] T004 [P] Create directory stubs: `backend/src/RepoManager.Api/Filters/` and `backend/src/RepoManager.Api/StartupValidators/` (add `.gitkeep` placeholders so directories are tracked)

**Checkpoint**: All new tests fail (red). TDD gate open for Phase 3.

---

## Phase 2: Foundational (Domain + Application + EF Core)

**Purpose**: Entity changes, DTOs, validators, EF Core configurations, and migrations that ALL subsequent user story phases depend on. Must be complete before any user story phase begins.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [X] T005 [P] Add `public string? ServiceOwner { get; set; }` to `backend/src/RepoManager.Domain/Entities/Repository.cs`
- [X] T006 [P] Add `public bool IsSystem { get; set; }` to `backend/src/RepoManager.Domain/Entities/ReleaseNoteTemplate.cs`
- [X] T007 [P] Add `string? ServiceOwner` field to `RepositoryDto` and `UpdateRepositoryRequest` in `backend/src/RepoManager.Application/Repositories/IRepositoryService.cs`
- [X] T008 [P] Add `bool IsSystem` field to `TemplateDto` in `backend/src/RepoManager.Application/Templates/IReleaseNoteTemplateService.cs`
- [X] T009 Create `RepoSummaryContext` record in `backend/src/RepoManager.Application/DTOs/Releases/RepoSummaryContext.cs` — fields: `Name`, `ServiceOwner` (string, never null), `PreviousVersion`, `NextVersion`, `CommitCount`, `TicketCount`
- [X] T010 Extend `ReleaseRenderContext` with `IReadOnlyList<RepoSummaryContext> Repositories { get; init; }` in `backend/src/RepoManager.Application/DTOs/Releases/RenderContextDtos.cs` — replaced `RepoContextDto`; updated `ReleaseRenderService.cs` accordingly
- [X] T011 Add `ServiceOwner` max-length validator rule (`MaximumLength(120).When(x => x.ServiceOwner is not null)`) to `backend/src/RepoManager.Application/Validators/UpdateRepositoryRequestValidator.cs`
- [X] T012 [P] Add `Task<TemplateDto> CloneAsync(Guid id, CancellationToken ct = default)` to `backend/src/RepoManager.Application/Templates/IReleaseNoteTemplateService.cs`
- [X] T013 Configure `ServiceOwner` column (nullable, maxLength 120) in `AppDbContext.cs` Repository entity section
- [X] T014 Configure `IsSystem` column (non-nullable, default `false`) in `AppDbContext.cs` ReleaseNoteTemplate entity section
- [X] T015 Create `ReleaseSummaryTemplateBody` static class with `const string Default` in `backend/src/RepoManager.Infrastructure/Persistence/SeedData/ReleaseSummaryTemplateBody.cs`
- [X] T016 Generated and applied migration `AddColumn_Repositories_ServiceOwner` (also captured `IsSystem` column addition)
- [X] T017 Generated migration `AddColumn_Templates_IsSystem` — manually added `migrationBuilder.InsertData(...)` for "Release Summary (Default)" seed row; applied both migrations successfully

**Checkpoint**: `dotnet build backend/src` succeeds with zero errors. Two migrations applied. Seed row for "Release Summary (Default)" exists in the database.

---

## Phase 3: User Story 1 — Operator Secures Initial Deployment (Priority: P1) 🎯 MVP

**Goal**: The `/auth/setup` endpoint is protected by a pre-shared key; the application refuses to start if the key is absent on first run; incorrect or missing keys return `401`; post-setup calls return `409`.

**Independent Test**: Deploy with key absent + empty DB → app refuses to start. Start with key set → call `POST /auth/setup` without header → `401`; wrong key → `401`; correct key → `201` admin created; second call → `409`.

### Implementation for User Story 1

- [ ] T018 [US1] Implement `SetupKeyStartupValidator` in `backend/src/RepoManager.Api/StartupValidators/SetupKeyStartupValidator.cs` — inject `IConfiguration` and `IServiceScopeFactory` and `IHostApplicationLifetime`; in `StartAsync` check key presence and user count; call `StopApplication()` and log Fatal if misconfigured; make T002 unit tests green
- [ ] T019 [US1] Implement `SetupKeyAuthorizationFilter` in `backend/src/RepoManager.Api/Filters/SetupKeyAuthorizationFilter.cs` — read `X-Setup-Key` header; compare using `CryptographicOperations.FixedTimeEquals`; short-circuit with `401 { "code": "setup_key_invalid" }` on mismatch; never log key value; make T001 unit tests green
- [ ] T020 [US1] Register services in `backend/src/RepoManager.Api/Program.cs`: `builder.Services.AddHostedService<SetupKeyStartupValidator>()` and `builder.Services.AddScoped<SetupKeyAuthorizationFilter>()`
- [ ] T021 [US1] Apply `[ServiceFilter(typeof(SetupKeyAuthorizationFilter))]` to the `Setup` action in `backend/src/RepoManager.Api/Controllers/AuthController.cs`
- [ ] T022 [US1] Configure `UseSerilogRequestLogging` in `backend/src/RepoManager.Api/Program.cs` to exclude `X-Setup-Key` from the enriched diagnostic context via `options.EnrichDiagnosticContext`
- [ ] T023 [P] [US1] Write integration tests in `backend/tests/RepoManager.IntegrationTests/Auth/SetupEndpointTests.cs` — covers all 5 acceptance criteria: no header → 401; wrong key → 401; correct key + empty DB → 201; correct key + existing user → 409; app startup with absent key + empty DB aborts (tested via validator unit test, not integration)

**Checkpoint**: `dotnet test backend/tests --filter "SetupKey"` — all tests green. `dotnet test backend/tests --filter "SetupEndpoint"` — all integration tests green.

---

## Phase 4: User Story 2 — Service Ownership on Repositories (Priority: P2)

**Goal**: Admins can set and clear a "Service Owner" text field (max 120 chars) on any repository. The value persists, appears in all repository API responses, and is available as a non-null string in all Handlebars template contexts.

**Independent Test**: Set ServiceOwner via `PUT /api/v1/repositories/{id}`; call `GET /api/v1/repositories/{id}` → confirm `"serviceOwner": "Platform Team"` in response; refresh the Settings → Repositories page → value is visible.

### Implementation for User Story 2

- [X] T024 [US2] Update `RepositoryService.cs` in `backend/src/RepoManager.Infrastructure/Services/RepositoryService.cs` to pass `ServiceOwner` through `MapToDto` projection and `UpdateAsync` persistence
- [X] T025 [US2] Verify `RepositoriesController` in `backend/src/RepoManager.Api/Controllers/RepositoriesController.cs` flows `ServiceOwner` through `PUT` and `GET` responses (Mapster auto-maps if DTO fields match; confirm no manual exclusion)
- [X] T026 [P] [US2] Write integration test in `backend/tests/RepoManager.IntegrationTests/Repositories/ServiceOwnerTests.cs` — set value via PUT → GET returns it; clear via PUT (null) → GET returns null; length 121 → 400
- [X] T027 [US2] Add "Service Owner" `Input` field to `frontend/src/features/settings/repositories/RepositoryEditPanel.tsx` — label "Service Owner", placeholder "e.g. Platform Team", `maxLength={120}`; render as read-only `<p>` for Viewer role; show "—" when null
- [X] T028 [P] [US2] Add ServiceOwner column to `frontend/src/features/settings/repositories/RepositoriesTable.tsx` — display value or "—" when null

**Checkpoint**: Admin edits ServiceOwner in the UI, saves, refreshes — value persists. Viewer sees it read-only. GET endpoint returns correct value.

---

## Phase 5: User Story 3 — Release Summary System Template (Priority: P2)

**Goal**: "Release Summary (Default)" is pre-seeded, immutable (admin can only clone it), auto-bound to every new project, and renders a per-repo table with `serviceOwner` values. All templates gain access to the `repositories` context variable.

**Independent Test**: `GET /api/v1/templates` returns the system template with `"isSystem": true`. `PUT` or `DELETE` on it → `403`. Clone it → editable copy created. Create a new project → it is auto-bound. Preview template for a 2-repo release → table has 2 rows with correct `serviceOwner`.

### Implementation for User Story 3

- [ ] T029 [US3] Implement `CloneAsync` in `backend/src/RepoManager.Infrastructure/Services/TemplateService.cs` — auto-increment naming algorithm (`(copy)`, `(copy 2)`, …), runs inside DB transaction; see `data-model.md` for naming logic
- [ ] T030 [US3] Guard `UpdateAsync` and `DeleteAsync` in `backend/src/RepoManager.Infrastructure/Services/TemplateService.cs` — throw `ForbiddenException("system_template_readonly")` when `template.IsSystem == true`
- [ ] T031 [US3] Extend `BuildContextAsync` in `backend/src/RepoManager.Infrastructure/Services/TemplateRenderingService.cs` — fetch `ReleaseRepository` join rows, project to `RepoSummaryContext` (coalesce `ServiceOwner ?? ""`), assign to `context.Repositories`
- [ ] T032 [US3] Update `CreateAsync` in `backend/src/RepoManager.Infrastructure/Services/ProjectService.cs` — after persisting the project, query for the system template (`IsSystem = true`, `Name = "Release Summary (Default)"`); if found, insert `ProjectTemplateBinding` with `Kind = TemplateBindingKind.ReleaseSummary`, `SortOrder = 1`, inside the same transaction
- [ ] T033 [US3] Add `POST /api/v1/templates/{id}/clone` endpoint to `backend/src/RepoManager.Api/Controllers/TemplatesController.cs` — call `ITemplateService.CloneAsync`; return `201 Created` with the new template DTO; `[Authorize(Roles = "Admin")]`
- [ ] T034 [P] [US3] Write integration tests in `backend/tests/RepoManager.IntegrationTests/Templates/TemplateSystemFlagTests.cs` — GET returns isSystem; PUT system template → 403; DELETE system template → 403; clone → 201 editable copy; clone twice → auto-incremented name
- [ ] T035 [P] [US3] Write integration test in `backend/tests/RepoManager.IntegrationTests/Templates/TemplateRenderContextTests.cs` — preview template for a 2-repo release returns `<table>` with 2 data rows and correct `serviceOwner` values (including "—" fallback when null)
- [ ] T036 [US3] Add `[System]` `Badge` (shadcn) to system template rows in `frontend/src/features/settings/templates/TemplatesTable.tsx` — render conditionally when `template.isSystem === true`
- [ ] T037 [US3] Swap Edit/Delete buttons for a single "Clone" button on system template rows in `frontend/src/features/settings/templates/TemplatesTable.tsx` — call `POST /api/v1/templates/{id}/clone`; show loading state; toast on success; refresh template list
- [ ] T038 [P] [US3] Verify in the frontend project creation flow that `frontend/src/features/settings/projects/ProjectFormSheet.tsx` (or equivalent) triggers a refetch of template bindings after creation — the auto-bound system template should appear without a page reload

**Checkpoint**: `GET /api/v1/templates` shows system template. `PUT`/`DELETE` on it → 403. Cloning works with auto-increment. New project has the system template auto-bound. Template preview shows per-repo table with ownership data.

---

## Phase 6: User Story 4 — Session Auto-Renewal (Priority: P2)

**Goal**: Users in long sessions never see a login redirect due to token expiry. The refresh token moves to an httpOnly cookie. The frontend silently renews the session 2 minutes before expiry (on login and on every app load). Concurrent 401s produce exactly one refresh call.

**Independent Test**: Shorten JWT lifetime to 30 s; log in; wait 28 s; make an API call → call succeeds with no redirect. Open DevTools → `document.cookie` does not contain the refresh token value.

### Implementation for User Story 6

- [ ] T039 [US4] Update `backend/src/RepoManager.Api/Controllers/AuthController.cs` — in the `Refresh` action: append `refreshToken` httpOnly cookie (`Secure`, `SameSite=Strict`, `Path=/api/v1/auth`, `MaxAge=30d`); read token from `Request.Cookies["refreshToken"]` instead of request body
- [ ] T040 [US4] Update `Login` action in `backend/src/RepoManager.Api/Controllers/AuthController.cs` — set the same httpOnly cookie on successful login response
- [ ] T041 [US4] Add `scheduleRefresh(accessToken: string)` action to `frontend/src/features/auth/authStore.ts` — decodes `exp` from JWT payload using `atob`; cancels existing timer; sets new `setTimeout` at `exp * 1000 - Date.now() - 120_000` ms; calls `POST /api/v1/auth/refresh` on fire
- [ ] T042 [US4] Call `scheduleRefresh(accessToken)` inside the `login()` action and inside the `onRehydrateStorage` hydration callback in `frontend/src/features/auth/authStore.ts`
- [ ] T043 [US4] Implement 401-intercept → refresh → retry-once interceptor with shared module-level `refreshPromise` in `frontend/src/lib/apiClient.ts` — flag `_retried` prevents loops; on refresh failure clear auth store, navigate to `/login`, show toast `"Your session has expired. Please log in again."`
- [ ] T044 [P] [US4] Write Playwright E2E test in `frontend/tests/e2e/session-renewal.spec.ts` — configure backend JWT lifetime to 30 s via env override; log in; wait 28 s; trigger API call; assert no redirect to `/login` and call succeeds

**Checkpoint**: `document.cookie` does not contain `refreshToken`. Near-expiry API call succeeds silently. Two simultaneous near-expiry calls produce exactly one `POST /auth/refresh` network request.

---

## Phase 7: User Story 5 — Delete Draft Releases (Priority: P3)

**Goal**: Admins can delete Draft releases from both the list view (kebab menu) and the detail page (header button), with an explicit confirmation step. Viewer role and Published releases show no delete option. Race conditions (409) and stale-open detail pages (404) are handled gracefully.

**Independent Test**: Log in as Admin; create a Draft release; click "Delete draft" from list → confirmation dialog appears; confirm → row fades out with toast. Log in as Viewer → no kebab menu visible. Try to navigate to a deleted release URL → "Release not found" message with back link.

### Implementation for User Story 5

- [ ] T046 [US5] Add `DropdownMenu` kebab trigger to Draft release rows in `frontend/src/features/releases/ReleasesTable.tsx` — render only when `release.status === "Draft"` AND `currentUser.role === "Admin"`; single menu item "Delete draft"
- [ ] T047 [US5] Wire "Delete draft" to `AlertDialog` confirmation in `frontend/src/features/releases/ReleasesTable.tsx` — dialog text: `"Delete draft release '<name>'? This cannot be undone."`; Cancel + Delete (destructive red) buttons
- [ ] T048 [US5] Handle DELETE outcomes in `frontend/src/features/releases/ReleasesTable.tsx` — success: fade-out row animation + toast `"Draft release '<name>' deleted."`; 409 conflict: toast `"This release has been published and can no longer be deleted."` + refresh row status; no blank screen on any error
- [ ] T049 [US5] Add "Delete draft" `Button` (destructive variant) to the header area of `frontend/src/features/releases/ReleaseDetailPage.tsx` — visible only when `release.status === "Draft"` AND `currentUser.role === "Admin"`; placed adjacent to the existing "Edit" button
- [ ] T050 [US5] Wire confirmation dialog and navigate-on-success in `frontend/src/features/releases/ReleaseDetailPage.tsx` — same dialog text as list view; on success: `navigate(`/projects/${release.projectId}/releases`)` with toast
- [ ] T051 [US5] Add 404 fallback to `frontend/src/features/releases/ReleaseDetailPage.tsx` — when the detail fetch returns 404 (or any DELETE/POST call returns 404), render "Release not found" message with a `Link` to `/projects/${projectId}/releases`

**Checkpoint**: Viewer sees no delete option anywhere. Admin sees kebab on Draft rows and header button on Draft detail page. Confirming deletion removes the release. 409 race and 404 stale-page both handled without crash.

---

## Final Phase: Polish & Cross-Cutting Concerns

**Purpose**: OpenAPI client regeneration, full test suite validation, smoke test execution, and task-guidance docs update.

- [ ] T052 Regenerate OpenAPI client: `cd frontend && npm run generate:api` — verify generated types include `serviceOwner` on `RepositoryDto`, `isSystem` on `ReleaseNoteTemplateDto`, and `POST /templates/{id}/clone` operation
- [ ] T053 [P] Run full backend test suite: `dotnet test backend/tests` — confirm all tests green including T001–T002 (unit), T023 (SetupEndpoint), T026 (ServiceOwner), T034–T035 (Templates)
- [ ] T054 Execute all 5 steps of the Milestone 13 smoke test from `specs/008-milestone-13-hardening/quickstart.md` — setup key enforcement, setup key protection, service owner in release summary, silent session renewal, delete draft UI
- [ ] T055 [P] Append Milestone 13 definition block to `docs/04-tasks-guidance.md` using the content from `docs/Feature_07/06-addendum-milestone-13.md` (the `/tasks` section)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately; write failing tests first.
- **Foundational (Phase 2)**: Depends on Phase 1 completion — BLOCKS all user story phases.
- **US1 (Phase 3)**: Depends on Phase 2 + Phase 1 TDD gate. No dependencies on US2–US5.
- **US2 (Phase 4)**: Depends on Phase 2. No dependencies on US1 or US3–US5.
- **US3 (Phase 5)**: Depends on Phase 2 + US2 (needs `ServiceOwner` in render context). Should follow US2.
- **US4 (Phase 6)**: Depends on Phase 2 only. No dependencies on US1–US3 or US5.
- **US5 (Phase 7)**: Depends on Phase 2 only. No dependencies on US1–US4.
- **Polish (Final)**: Depends on all desired user story phases complete.

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2 — no story dependencies.
- **US2 (P2)**: Can start after Phase 2 — no story dependencies.
- **US3 (P2)**: Should follow US2 (relies on `ServiceOwner` field being available in render context).
- **US4 (P2)**: Can start after Phase 2 — no story dependencies.
- **US5 (P3)**: Can start after Phase 2 — no story dependencies.

### Within Each User Story

- TDD tests written and confirmed failing BEFORE any Phase 3 implementation.
- Domain/Application changes (Phase 2) complete before service implementations.
- Backend service implementations complete before frontend components consume them.
- OpenAPI client regenerated (T052) before any frontend work that needs new generated types.

---

## Parallel Execution Examples

### Phase 2 (Foundational) — can run concurrently:
```
T005 Add ServiceOwner to Repository entity
T006 Add IsSystem to ReleaseNoteTemplate entity
T007 Add ServiceOwner to RepositoryDto/UpdateRepositoryRequest
T008 Add IsSystem to ReleaseNoteTemplateDto
```

### Phase 3 (US1) + Phase 4 (US2) — independent stories, can overlap after Phase 2:
```
Developer A: T018 → T019 → T020 → T021 → T022 (US1 backend)
Developer B: T024 → T025 → T027 → T028 (US2 backend + frontend)
```

### Phase 5 (US3) integration tests — can run in parallel:
```
T034 TemplateSystemFlagTests.cs
T035 TemplateRenderContextTests.cs
```

### Phase 6 (US4) + Phase 7 (US5) — backend and frontend work independent of each other:
```
Developer A: T039 → T040 → T041 → T042 → T043 (US4)
Developer B: T046 → T047 → T048 → T049 → T050 → T051 (US5)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: TDD gate (write failing tests).
2. Complete Phase 2: Foundational entity/DTO/migration changes.
3. Complete Phase 3: Setup key protection (US1).
4. **STOP and VALIDATE**: `POST /auth/setup` rejects bad keys, accepts good key, app refuses to start without key.
5. Deploy/demo if ready — core security hardening is live.

### Incremental Delivery

1. Phase 1 + Phase 2 → Foundation ready.
2. Phase 3 → Setup key protection live. Test independently.
3. Phase 4 → ServiceOwner field live. Test independently.
4. Phase 5 → Release Summary template live. Test independently. (Depends on US2 being done.)
5. Phase 6 → Session auto-renewal live. Test independently.
6. Phase 7 → Delete Draft UI live. Test independently.
7. Final Phase → Full smoke test. Ship Milestone 13.

---

## Notes

- `[P]` tasks operate on different files and have no shared dependencies within their phase.
- `[US#]` label maps each task to the user story it delivers — use for traceability during code review.
- Each user story phase is independently completable and testable before moving to the next.
- TDD gate (Phase 1) is mandatory per the constitution — failing tests must exist before Phase 3 code is written.
- The backend integration tests for US3 (T034, T035) are the highest-value tests in this milestone: they verify the seed migration, system-template guard, clone logic, and render context extension in a single pass.
- OpenAPI client regeneration (T052) must happen before any frontend task that consumes new types — regenerate after all backend changes are stable.
