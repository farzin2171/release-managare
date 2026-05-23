---

description: "Task list for Per-Repo Release Versioning feature implementation"
---

# Tasks: Per-Repo Release Versioning

**Feature branch**: `006-per-repo-release-versioning`  
**Input**: Design documents from `specs/005-per-repo-release-versioning/`  
**Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Data model**: [data-model.md](data-model.md)  
**Contracts**: [contracts/api-endpoints.md](contracts/api-endpoints.md) | [contracts/service-interfaces.md](contracts/service-interfaces.md)

**Tests**: Unit tests are required (TDD gate — constitution principle III). Integration tests included for the full create-release flow and backfill migration.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no blocking dependencies)
- **[Story]**: Maps to user story label (US1–US4) from spec.md
- Exact file paths included in every task

---

## Phase 1: Setup

**Purpose**: Extend the Domain layer with the new `ReleaseRepository` entity before any other work begins.

- [ ] T001 Add `ReleaseRepository.cs` POCO entity with all snapshot columns (`Id`, `ReleaseId`, `RepositoryId`, `PreviousVersion`, `NextVersion`, `BumpType`, `FromCommitSha`, `ToCommitSha`, `CommitCount`, `TicketCount`) in `backend/src/RepoManager.Domain/Entities/ReleaseRepository.cs`
- [ ] T002 [P] Update `Release.cs` to add `ICollection<ReleaseRepository> ReleaseRepositories` navigation property in `backend/src/RepoManager.Domain/Entities/Release.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before any user story work begins — TDD unit tests, `IVersionBumpService`, EF Core config and migration, DI wiring.

**⚠️ CRITICAL**: T003 and T004 must be written and **confirmed failing** before T007 and T014 respectively (TDD gate per constitution principle III).

- [ ] T003 Write failing unit tests for `VersionBumpService.SuggestAsync` covering: breaking change `BREAKING CHANGE` footer → major, breaking `!` syntax → major, `feat` only → minor, `fix` only → patch, no commits since tag → patch/0 counts, no semver tag → `PreviousVersion=""` + `SuggestedNextVersion="0.1.0"` in `backend/tests/RepoManager.UnitTests/Services/VersionBumpServiceTests.cs`
- [ ] T004 Write failing unit tests for `DeriveReleaseVersion` covering: primary repo included → uses primary's `NextVersion`, primary repo excluded → falls back to alphabetically-first included repo, single repo included → uses that repo's `NextVersion` in `backend/tests/RepoManager.UnitTests/Services/ReleaseCompositionServiceTests.cs`
- [ ] T005 [P] Create `IVersionBumpService.cs` interface and `VersionBumpSuggestionDto` record (fields: `PreviousVersion`, `SuggestedNextVersion`, `BumpType`, `FromCommitSha`, `ToCommitSha`, `CommitCount`, `TicketCount`) in `backend/src/RepoManager.Application/Services/IVersionBumpService.cs` and `backend/src/RepoManager.Application/DTOs/Releases/VersionBumpSuggestionDto.cs`
- [ ] T006 [P] Create `ReleaseRepositoryConfiguration.cs` with unique index on `(ReleaseId, RepositoryId)`, non-unique index on `RepositoryId`, cascade delete from `Release`, restrict delete from `Repository` in `backend/src/RepoManager.Infrastructure/Persistence/EntityConfigurations/ReleaseRepositoryConfiguration.cs`
- [ ] T007 Implement `VersionBumpService.cs` using `IConventionalCommitParser` — scan commit range since latest semver tag, apply bump-type derivation rules in priority order (breaking > feat > fix), set edge-case outputs for no-tag and no-commit scenarios; must make T003 tests pass in `backend/src/RepoManager.Infrastructure/Services/VersionBumpService.cs`
- [ ] T008 Update `AppDbContext.cs` to add `DbSet<ReleaseRepository> ReleaseRepositories` and register `ReleaseRepositoryConfiguration` in `OnModelCreating` in `backend/src/RepoManager.Infrastructure/Persistence/AppDbContext.cs`
- [ ] T009 Add `AddReleaseRepository` EF Core migration — `CreateTable("ReleaseRepositories")`, two indexes, FK constraints (cascade/restrict), then raw backfill SQL inserting one legacy row per existing `Release` joining to `Projects.PrimaryRepositoryId` with `BumpType="manual"` and empty snapshot fields in `backend/src/RepoManager.Infrastructure/Persistence/Migrations/`
- [ ] T010 Register `IVersionBumpService` → `VersionBumpService` as scoped in the DI container in `backend/src/RepoManager.Api/Program.cs`

**Checkpoint**: Domain entity exists, migration ready, `IVersionBumpService` implemented and tested — user story work can now begin.

---

## Phase 3: User Story 1 — Multi-Repo Release Composition (Priority: P1) 🎯 MVP

**Goal**: Admin can create a Draft release by selecting a subset of project repos, confirming per-repo next versions, and submitting. Snapshot fields are captured server-side. Draft releases are updatable. Published releases are locked.

**Independent Test**: Create a project with 3 repos (each with at least one commit since its last tag), open the release creation wizard, select 2 repos and enter distinct `NextVersion` values manually, submit, and verify the saved release has a `releaseRepositories` array with the correct `nextVersion`, `bumpType`, and non-empty snapshot SHAs for each selected repo.

- [ ] T011 [P] [US1] Add release-composition DTOs as records: `CreateReleaseRequest`, `UpdateReleaseRequest`, `ReleaseRepositorySelectionDto` (fields: `RepositoryId`, `NextVersion`, `BumpType`), `ReleaseDto`, `ReleaseRepositoryDto` (including `IsLegacy` flag) in `backend/src/RepoManager.Application/DTOs/Releases/`
- [ ] T012 [P] [US1] Create `CreateReleaseRequestValidator.cs` — validate `Name` not empty (max 200 chars), `Repositories` not empty (error code `at_least_one_repo_required`), each `NextVersion` is valid semver (`invalid_semver`), each `BumpType` is one of `major|minor|patch|manual` (`invalid_bump_type`) in `backend/src/RepoManager.Application/Validators/CreateReleaseRequestValidator.cs`
- [ ] T013 [P] [US1] Create `IReleaseCompositionService.cs` with `PreviewAsync(int projectId, IReadOnlyList<int> repositoryIds, ct)`, `CreateDraftAsync(int projectId, CreateReleaseRequest, ct)`, `UpdateDraftAsync(int releaseId, UpdateReleaseRequest, ct)` signatures and exception contract comments in `backend/src/RepoManager.Application/Services/IReleaseCompositionService.cs`
- [ ] T014 [US1] Implement `ReleaseCompositionService.cs` with `CreateDraftAsync` (explicit transaction, validate repos belong to project, call `IVersionBumpService.SuggestAsync` per repo, call `DeriveReleaseVersion` for `Release.Version`, write `Release` + `ReleaseRepository` rows atomically) and `UpdateDraftAsync` (Draft guard throwing `ConflictException("release_not_draft")`, wholesale replace `ReleaseRepository` collection, re-capture snapshot); must make T004 `DeriveReleaseVersion` tests pass in `backend/src/RepoManager.Infrastructure/Services/ReleaseCompositionService.cs`
- [ ] T015 [US1] Register `IReleaseCompositionService` → `ReleaseCompositionService` as scoped and wire `CreateReleaseRequestValidator` with `AddFluentValidation` in DI in `backend/src/RepoManager.Api/Program.cs`
- [ ] T016 [US1] Create `ReleasesController.cs` with `[Authorize]` on class and `[Authorize(Roles = "Admin")]` on write endpoints — implement `POST /api/v1/projects/{projectId}/releases` (201 Created), `GET /api/v1/projects/{projectId}/releases/{id}` (200 with full `ReleaseDto` including `releaseRepositories`), `PUT /api/v1/projects/{projectId}/releases/{id}` (200 updated DTO, 409 on `release_not_draft`), `DELETE /api/v1/projects/{projectId}/releases/{id}` (204, 409 on non-Draft) in `backend/src/RepoManager.Api/Controllers/ReleasesController.cs`
- [ ] T017 [US1] Add pessimistic edit lock — create `AddReleaseLockColumns` EF migration adding `EditLockedByUserId int?`, `EditLockExpiresAt datetime?`, `EditLockedByUserName nvarchar(200)?` to `Releases`; implement `AcquireEditLockAsync` (called on GET with `?mode=edit`, blocks if lock held by another user and TTL not expired), `ReleaseEditLockAsync`, and TTL-check in `UpdateDraftAsync` (refresh lock on each save); return 409 with lock-holder name when blocked in `backend/src/RepoManager.Infrastructure/Services/ReleaseCompositionService.cs` and `backend/src/RepoManager.Api/Controllers/ReleasesController.cs`
- [ ] T018 [US1] Block repository removal from a project when the repo is included in any Draft release for that project — add guard check in the existing project/repository unassignment service method throwing error code `repo_in_draft_release` in `backend/src/RepoManager.Infrastructure/Services/`
- [ ] T019 [US1] Regenerate the frontend OpenAPI client once the backend is runnable via `npm run generate-client` from `frontend/`
- [ ] T020 [P] [US1] Create `ReleaseRepoSelectionStep.tsx` wizard step — repo checklist (pre-selected based on `hasChanges`), per-repo `NextVersion` text input, `BumpType` radio group (`major|minor|patch|manual`), live-derived release label showing the version source repo name, Zod validation for `invalid_semver` and `version_not_greater`, zero-repo-selected validation message, "no tag" indicator when `PreviousVersion` is empty in `frontend/src/features/releases/components/wizard/ReleaseRepoSelectionStep.tsx`
- [ ] T021 [US1] Write integration test for full create-release flow using a real per-test SQLite file — call `POST /preview`, `POST /releases` (assert 201 + `releaseRepositories` snapshot fields populated), `GET /releases/{id}` (assert snapshot unchanged), `PUT /releases/{id}` (assert 200 + updated snapshot), `DELETE /releases/{id}` (assert 204) in `backend/tests/RepoManager.IntegrationTests/Releases/CreateReleaseFlowTests.cs`
- [ ] T022 [US1] Write integration test for `AddReleaseRepository` backfill migration — seed a `Release` row before migration, run `Up()`, assert a `ReleaseRepository` row exists with `BumpType="manual"` and empty snapshot fields, run `Up()` again and assert no duplicates (idempotency) in `backend/tests/RepoManager.IntegrationTests/Releases/BackfillMigrationTests.cs`

**Checkpoint**: US1 is fully functional — draft releases can be created, updated, and deleted with per-repo snapshot data.

---

## Phase 4: User Story 2 — Automatic Version Suggestion per Repo (Priority: P2)

**Goal**: The release creation wizard pre-fills each repo's suggested next version and bump type based on its commit history. Changing the bump type radio recomputes the version field; manual version edits switch the radio to `custom`.

**Independent Test**: Load the release wizard for a project where repos have commits since their last semver tag; verify each repo row shows a pre-filled `SuggestedNextVersion` and `BumpType` derived from the dominant commit type in its range.

- [ ] T023 [P] [US2] Add `ReleasePreviewDto` (fields: `Repositories`, `DerivedReleaseVersion`, `DerivedFromRepositoryId`) and `ReleasePreviewRepoDto` (fields: `RepositoryId`, `Name`, `IsPrimary`, `HasChanges`, `PreviousVersion`, `SuggestedNextVersion`, `BumpType`, `CommitCount`, `TicketCount`) records in `backend/src/RepoManager.Application/DTOs/Releases/`
- [ ] T024 [US2] Implement `PreviewAsync` in `ReleaseCompositionService.cs` — validate all `repositoryIds` belong to the project, call `IVersionBumpService.SuggestAsync` per repo in parallel, map to `ReleasePreviewRepoDto`, call `DeriveReleaseVersion` to compute `DerivedReleaseVersion` and `DerivedFromRepositoryId` in `backend/src/RepoManager.Infrastructure/Services/ReleaseCompositionService.cs`
- [ ] T025 [US2] Add `POST /api/v1/projects/{projectId}/releases/preview` endpoint (Admin role, read-only, 200 with `ReleasePreviewDto`, 400 on unknown repo IDs, 404 on project not found) to `ReleasesController.cs` in `backend/src/RepoManager.Api/Controllers/ReleasesController.cs`
- [ ] T026 [US2] Create `useReleasePreview.ts` TanStack Query mutation hook wrapping `POST /preview`; call on wizard mount with the project's full list of assigned repo IDs in `frontend/src/features/releases/hooks/useReleasePreview.ts`
- [ ] T027 [US2] Integrate preview data from `useReleasePreview` into `ReleaseRepoSelectionStep.tsx` — pre-fill `NextVersion` and `BumpType` from suggestion, recalculate `NextVersion` when bump type radio changes (major/minor/patch increment from `PreviousVersion`), switch radio to `custom` when user manually edits the version field in `frontend/src/features/releases/components/wizard/ReleaseRepoSelectionStep.tsx`

**Checkpoint**: Wizard now pre-fills version suggestions; US1 and US2 together are fully functional end-to-end.

---

## Phase 5: User Story 4 — Historical Snapshot on Release Detail (Priority: P2)

**Goal**: Release detail page shows each included repo's immutable snapshot (previous → next version, bump type, commit/ticket counts). Legacy backfilled rows display em-dash placeholders and a "Pre-feature release — partial data" badge.

**Independent Test**: View a published release detail page and verify the per-repo table shows correct snapshot values; delete the underlying repo tag and reload the page — values must be unchanged.

- [ ] T028 [P] [US4] Create `ReleaseRepositoriesTable.tsx` — table with columns: Repo Name, Previous Version, Next Version, Bump Type badge (`major`/`minor`/`patch`/`manual`), Commit Count, Ticket Count; when `isLegacy=true` render em-dash placeholders for snapshot fields and a "Pre-feature release — partial data" badge on the row in `frontend/src/features/releases/components/ReleaseRepositoriesTable.tsx`
- [ ] T029 [US4] Add `<ReleaseRepositoriesTable>` section to the release detail page component, fed from the `releaseRepositories` array on the `ReleaseDto` returned by `GET /releases/{id}` in `frontend/src/features/releases/`

**Checkpoint**: Release detail page shows immutable per-repo snapshots for both new and legacy releases.

---

## Phase 6: User Story 3 — Releases List on Project Page (Priority: P3)

**Goal**: Project detail page has a Releases section listing all releases sorted by most-recently-created, filterable by status, searchable by name.

**Independent Test**: Navigate to a project with 5 releases in various statuses; verify the Releases section lists all 5; filter by `Published`; verify only published releases appear; click a row; verify navigation goes to the release detail page.

- [ ] T030 [US3] Add `GET /api/v1/projects/{projectId}/releases` endpoint (Viewer role) with query params `?status`, `?search` (case-insensitive substring on Name), `?sort=createdAt|name`, `?order=asc|desc` (default: `createdAt desc`); return array of summary DTOs with `id`, `name`, `version`, `status`, `createdAt`, `publishedAt`, `repoCount` in `backend/src/RepoManager.Api/Controllers/ReleasesController.cs`
- [ ] T031 [US3] Create `ProjectReleasesList.tsx` with TanStack Query driving the `GET /releases` list, status filter dropdown (All / Draft / Published / Archived), name search input (debounced), rows sorted by creation date descending, each row links to the release detail page in `frontend/src/features/releases/components/ProjectReleasesList.tsx`
- [ ] T032 [US3] Add a Releases tab (or section) to the project detail page and wire `<ProjectReleasesList projectId={id} />` to it in `frontend/src/features/` (project detail page component)

**Checkpoint**: All four user stories are fully functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Error-mapping completeness, observability, and end-to-end smoke validation.

- [ ] T033 [P] Verify the global `IExceptionHandler` maps `ConflictException("release_not_draft")` → 409 with `{ "code": "release_not_draft" }` and `ValidationException("repo_in_draft_release")` → 400; add missing cases if not already covered in `backend/src/RepoManager.Api/Middleware/`
- [ ] T034 [P] Add structured Serilog log entries to `VersionBumpService.SuggestAsync` — log `repositoryId`, elapsed duration (ms), `bumpType` result, and outcome (`success` or error code) at `Debug` level in `backend/src/RepoManager.Infrastructure/Services/VersionBumpService.cs`
- [ ] T035 Run the smoke test from `quickstart.md` end-to-end — create project with 3 repos, open wizard, verify suggestions, deselect one repo, submit, verify release detail, attempt to edit a Published release and confirm the edit is blocked

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — T003/T004 (TDD tests) must be written and failing before T007/T014
- **US1 (Phase 3)**: Depends on Phase 2 completion — BLOCKS all other user stories
- **US2 (Phase 4)**: Depends on Phase 3 (backend `IVersionBumpService` and `ReleaseCompositionService` exist)
- **US4 (Phase 5)**: Depends on Phase 3 (`GET /releases/{id}` returns full `ReleaseDto`) — can run in parallel with Phase 4
- **US3 (Phase 6)**: Depends on Phase 3 (`ReleasesController` exists) — can start after Phase 3
- **Polish (Phase 7)**: Depends on all user story phases being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Phase 2 complete — no dependency on other stories
- **US2 (P2)**: Depends on US1 backend (`IVersionBumpService`, `ReleaseCompositionService`, OpenAPI client) — can run parallel with US4 frontend
- **US4 (P2)**: Backend done in US1 (T016 `GET /releases/{id}`); frontend (T028–T029) can run parallel with US2
- **US3 (P3)**: Backend (T030) independent of US2/US4; frontend (T031–T032) depends on T030

### Within Each User Story

- TDD tests (T003, T004) MUST be written and FAIL before implementations (T007, T014)
- DTOs/interfaces before service implementations
- Service implementations before controller endpoints
- Backend endpoints before frontend components (OpenAPI client must be regenerated after backend is runnable)
- T019 (regenerate client) blocks all frontend tasks in US1+

### Parallel Opportunities

- T001 and T002 can run in parallel
- T003, T004, T005, T006 can all run in parallel (different files)
- T011, T012, T013 [P] can run in parallel within US1
- T021, T022 [P] integration tests can run in parallel
- T023 [P] (preview DTOs) can run in parallel with T024/T025 setup
- T028 [P] (ReleaseRepositoriesTable) can run in parallel with US2 work
- T033, T034 [P] can run in parallel in the polish phase

---

## Parallel Example: Phase 2 (Foundational)

```
# Launch all parallel foundational tasks together:
Task T003: Write VersionBumpServiceTests (6 scenarios)
Task T004: Write ReleaseCompositionServiceTests.DeriveReleaseVersion (3 scenarios)
Task T005: Create IVersionBumpService interface + VersionBumpSuggestionDto
Task T006: Create ReleaseRepositoryConfiguration

# Then sequentially:
T007: Implement VersionBumpService (make T003 pass)
T008: Update AppDbContext
T009: Add migration with backfill SQL
T010: Register DI
```

## Parallel Example: User Story 1

```
# Launch parallel US1 setup tasks together:
Task T011 [P] [US1]: Add all release-composition DTOs
Task T012 [P] [US1]: CreateReleaseRequestValidator
Task T013 [P] [US1]: IReleaseCompositionService interface

# Then sequentially:
T014: Implement ReleaseCompositionService (CreateDraftAsync + UpdateDraftAsync)
T015: Register DI
T016: ReleasesController (POST, GET, PUT, DELETE)
T017: Pessimistic edit lock
T018: Block repo unassignment guard
T019: Regenerate OpenAPI client

# Then parallel:
Task T021: CreateReleaseFlowTests integration test
Task T022: BackfillMigrationTests integration test
Task T020 [P] [US1]: ReleaseRepoSelectionStep.tsx (frontend)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001–T002)
2. Complete Phase 2: Foundational (T003–T010) — write tests first, confirm failing
3. Complete Phase 3: US1 (T011–T022)
4. **STOP and VALIDATE**: Run `dotnet test --filter "FullyQualifiedName~ReleaseComposition|VersionBump"`, then run the T035 smoke test manually
5. Demo with multi-repo release creation working end-to-end

### Incremental Delivery

1. Phase 1 + Phase 2 → Foundation ready with migration and version bump logic
2. Phase 3 (US1) → Draft release CRUD + wizard submission (MVP)
3. Phase 4 (US2) → Wizard now shows pre-filled version suggestions
4. Phase 5 (US4) → Release detail shows per-repo snapshot table
5. Phase 6 (US3) → Project page shows full release history list
6. Phase 7 → Observability, error mapping, smoke test sign-off

### Parallel Team Strategy

With two developers after Phase 2:
- **Developer A**: US1 backend (T011–T018) → US2 backend (T023–T025)
- **Developer B**: US1 frontend after T019 (T020) → US4 frontend (T028–T029) → US3 frontend (T031–T032)

---

## Notes

- `[P]` tasks target different files with no cross-task dependencies — safe to parallelize
- `[US1]`–`[US4]` labels map each task to its user story for traceability and independent delivery
- T003 and T004 are **TDD gate tasks** — they must be written and confirmed failing before their corresponding implementations (T007, T014) are started
- T009 backfill SQL must be idempotent — running `Up()` twice must not produce duplicate rows
- Snapshot fields (`PreviousVersion`, `FromCommitSha`, `ToCommitSha`, `CommitCount`, `TicketCount`) are **always re-derived server-side**; any client-supplied values for these fields are discarded
- For legacy releases (backfilled rows), `IsLegacy = true` when all snapshot string fields are empty
- The pessimistic lock (T017) uses a 10-minute TTL refreshed on each `PUT` — implement as columns on `Releases`, not an in-memory cache, so it survives app restarts
