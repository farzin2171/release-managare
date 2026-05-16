# Tasks: Latest Tag Selection for Repositories

**Input**: Design documents from `/specs/002-latest-tag-selection/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: Included — domain unit tests are mandatory per Constitution Principle III (TDD for all domain logic). API integration tests, WireMock infrastructure test, and one Playwright E2E are required per the implementation plan.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no blocking dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Exact file paths are included in every task description

## Path Conventions

```
backend/src/RepoManager.Domain/              ← entities, value objects, enums
backend/src/RepoManager.Application/         ← interfaces, DTOs, validators
backend/src/RepoManager.Infrastructure/      ← EF Core, service implementations, providers
backend/src/RepoManager.Api/                 ← controllers, DI, middleware
backend/tests/RepoManager.UnitTests/         ← domain unit tests
backend/tests/RepoManager.IntegrationTests/  ← API + infrastructure integration tests
frontend/src/features/repositories/          ← settings-side repository components
frontend/src/features/projects/              ← project detail screen components
frontend/src/lib/                            ← generated API client
frontend/tests/                              ← Vitest unit + Playwright E2E
```

---

## Phase 1: Setup

**Purpose**: Apply the schema migration before any other work begins.

- [ ] T001 Create and apply EF Core migration `AddLatestTagToRepositories` — run `dotnet ef migrations add AddLatestTagToRepositories --project backend/src/RepoManager.Infrastructure --startup-project backend/src/RepoManager.Api` then `dotnet ef database update` — verify four new nullable columns exist on `Repositories` table with no data backfill

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain layer primitives, interface contracts, and EF Core configuration that every user story phase depends on.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T002 [P] Add `RepositoryTag` sealed record `(string Name, string CommitSha, DateTimeOffset? CommitDate, string? AuthorName)` to `backend/src/RepoManager.Domain/ValueObjects/RepositoryTag.cs`
- [ ] T003 [P] Write failing TDD unit tests (Red phase) for `Repository.PinLatestTag` and `Repository.ClearLatestTag` — cover: untracked repo throws `ValidationException`; tracked repo sets all four fields; clear nulls all four fields — in `backend/tests/RepoManager.UnitTests/Domain/RepositoryLatestTagTests.cs`
- [ ] T004 Add `LatestTag`, `LatestTagCommitSha`, `LatestTagSetAt`, `LatestTagSetByUserId` properties and `PinLatestTag(string tagName, string commitSha, Guid userId, DateTime utcNow)` / `ClearLatestTag(Guid userId, DateTime utcNow)` methods to `backend/src/RepoManager.Domain/Entities/Repository.cs` — verify T003 tests now pass (Green)
- [ ] T005 [P] Add `AuthorName` field to the existing `TagInfo` record and add `ListTagsAsync(Guid repositoryId, CancellationToken ct = default)` signature to `backend/src/RepoManager.Application/GitProviders/IGitProviderService.cs`
- [ ] T006 [P] Add `GetTagsAsync`, `SetLatestTagAsync`, and `ClearLatestTagAsync` method signatures (as specified in `contracts/service-interfaces.md`) to `backend/src/RepoManager.Application/Repositories/IRepositoryService.cs`
- [ ] T007 [P] Extend `backend/src/RepoManager.Application/Repositories/Dtos/RepositoryDto.cs` with four new nullable fields (`LatestTag`, `LatestTagCommitSha`, `LatestTagSetAt`, `LatestTagSetBy`) and add `UserSummaryDto(Guid Id, string Email)` record to the same file
- [ ] T008 Update `backend/src/RepoManager.Infrastructure/Persistence/Configurations/RepositoryConfiguration.cs` — add column max-length mappings for `LatestTag` (255), `LatestTagCommitSha` (64); add `HasOne(r => r.LatestTagSetBy).WithMany().HasForeignKey(r => r.LatestTagSetByUserId).OnDelete(DeleteBehavior.SetNull)` navigation

**Checkpoint**: Run `dotnet build backend/src` — must compile cleanly. Run T003's tests — must be green.

---

## Phase 3: User Story 1 — Admin Pins a Latest Tag (Priority: P1) 🎯 MVP

**Goal**: Admin can open a tracked repository's detail panel from Settings → Repositories, fetch the live tag list, select a tag, pin it, and clear it. The panel reflects the pinned state immediately.

**Independent Test**: Log in as Admin → Settings → Repositories → click a tracked repo row → sheet opens → "Fetch tags" → list appears → select a tag → Confirm → toast → panel shows new pinned tag. Clear → confirmation → "Not set". Log in as Viewer — sheet shows tag read-only with no action buttons.

### Implementation for User Story 1

- [ ] T009 [US1] Implement `AzureDevOpsGitProvider.ListTagsAsync` in `backend/src/RepoManager.Infrastructure/GitProviders/AzureDevOpsGitProvider.cs` — call `GET /_apis/git/repositories/{externalId}/refs?filter=tags&peelTags=true&api-version=7.1`; map `peeledObjectId` → `CommitSha` (fallback to `objectId` for lightweight tags); batch commit-detail lookups via `Task.WhenAll` capped at 200; wrap in existing Polly retry policy; log `repository.tags.fetched` with duration, tag count, and outcome
- [ ] T010 [US1] Implement `RepositoryService.GetTagsAsync` in `backend/src/RepoManager.Infrastructure/Repositories/RepositoryService.cs` — look up repository, throw `NotFoundException` if missing, throw `ValidationException` if `!IsTracked`, delegate to `IGitProviderService.ListTagsAsync`, map `TagInfo` → `RepositoryTag`
- [ ] T011 [P] [US1] Create `SetLatestTagDto(string TagName)` record and `SetLatestTagDtoValidator` (TagName required, max 250 chars) in `backend/src/RepoManager.Application/Repositories/Dtos/SetLatestTagDto.cs`
- [ ] T012 [US1] Implement `RepositoryService.SetLatestTagAsync` in `backend/src/RepoManager.Infrastructure/Repositories/RepositoryService.cs` — re-fetch live tags via `GetTagsAsync`, throw `ValidationException` if tag name not found in fresh list, call `repository.PinLatestTag(tag.CommitSha, userId, DateTime.UtcNow)`, save changes, write audit entry via `IAuditLogger` (old value → new value), return updated `RepositoryDto`
- [ ] T013 [US1] Implement `RepositoryService.ClearLatestTagAsync` in `backend/src/RepoManager.Infrastructure/Repositories/RepositoryService.cs` — load repository (throw `NotFoundException` if missing), call `repository.ClearLatestTag(userId, DateTime.UtcNow)`, save changes, write audit entry (old value → null); method is idempotent — succeeds when no tag is pinned
- [ ] T014 [US1] Update `RepositoryService` DTO mapping (Mapster config or manual map) to populate the four new `RepositoryDto` fields; resolve `LatestTagSetBy` as `UserSummaryDto` from `LatestTagSetByUserId` FK — return `null` when FK is null and display "Unknown user" label when the user ID is set but no matching `Users` row exists (per FR-013 clarification)
- [ ] T015 [P] [US1] Add `GetTags` action (`[HttpGet("{id}/tags")]`, `[Authorize]`) to `backend/src/RepoManager.Api/Controllers/RepositoriesController.cs` — call `_repositories.GetTagsAsync`, return `200 { tags: [...] }`, map `NotFoundException` → 404, `ValidationException` → 422
- [ ] T016 [P] [US1] Add `SetLatestTag` action (`[HttpPut("{id}/latest-tag")]`, `[Authorize(Roles = "Admin")]`) to `backend/src/RepoManager.Api/Controllers/RepositoriesController.cs` — validate body with `SetLatestTagDto`, call `_repositories.SetLatestTagAsync`, return `200 RepositoryDto`, map errors to 404 / 422
- [ ] T017 [P] [US1] Add `ClearLatestTag` action (`[HttpDelete("{id}/latest-tag")]`, `[Authorize(Roles = "Admin")]`) to `backend/src/RepoManager.Api/Controllers/RepositoriesController.cs` — call `_repositories.ClearLatestTagAsync`, return `204 No Content`, map `NotFoundException` → 404
- [ ] T018 [P] [US1] Write API integration tests in `backend/tests/RepoManager.IntegrationTests/Api/RepositoryLatestTagTests.cs` — cover: Admin fetches tags (200), Admin pins tag (200), Admin clears tag (204), Viewer GET tags (200), Viewer PUT forbidden (403), Viewer DELETE forbidden (403), non-existent tag name returns 422, untracked repo returns 422, unknown repo returns 404
- [ ] T019 [US1] Regenerate frontend API client — run `npm run codegen` from `frontend/` to update `frontend/src/lib/api.d.ts` with the three new endpoints and updated `RepositoryDto` fields; confirm TypeScript compilation passes with `npm run build`
- [ ] T020 [US1] Add `getRepositoryTags(id: string)`, `setLatestTag(id: string, tagName: string)`, `clearLatestTag(id: string)` functions to `frontend/src/features/repositories/api/repositoriesApi.ts` using the generated API client
- [ ] T021 [P] [US1] Build `TagPickerDialog.tsx` in `frontend/src/features/repositories/components/TagPickerDialog.tsx` — shadcn `Dialog` containing a `DataTable` of `RepositoryTag` rows sorted by `commitDate` descending by default; name-search filter; row selection; Confirm button (disabled until a row is selected) calls `setLatestTag` mutation; on success invalidate `['repository', id]` and `['project', projectId]` query keys, close dialog, show success toast; TanStack Query key `['repository', id, 'tags']` with `staleTime: 0`
- [ ] T022 [P] [US1] Build `RepositoryDetailSheet.tsx` in `frontend/src/features/repositories/components/RepositoryDetailSheet.tsx` — shadcn `Sheet`; shows currently pinned tag name with "Last set: X ago by Y" metadata or "Not set" if null; "Set latest tag" button opens `TagPickerDialog`; "Clear" button (shown only when a tag is pinned) opens shadcn `AlertDialog` for confirmation then calls `clearLatestTag` mutation; Viewer role sees tag read-only with no action buttons
- [ ] T023 [US1] Make repository table rows in `frontend/src/features/repositories/RepositoriesPage.tsx` clickable — clicking a row opens `RepositoryDetailSheet` for that repository; the sheet receives `repositoryId` and `projectId` props for query invalidation

**Checkpoint**: User Story 1 is independently functional. Run the manual smoke test from `quickstart.md` steps 6–7.

---

## Phase 4: User Story 2 — Viewer Sees Latest Tag on Project Screen (Priority: P2)

**Goal**: Any authenticated user can see the pinned latest tag for each repository in the project detail screen's repositories table, with a tooltip and amber indicator for unset repos.

**Independent Test**: Admin pins a tag for a repo in a project → Viewer opens the project detail screen → "Latest tag" column shows the pinned tag badge → hover shows tooltip with SHA, date, email → a repo with no pinned tag shows "—" with an amber dot.

### Implementation for User Story 2

- [ ] T024 [US2] Add "Latest tag" column to `frontend/src/features/projects/components/ProjectRepositoriesTable.tsx` — render `<Badge variant="outline" className="font-mono">{latestTag}</Badge>` when pinned or `<span className="text-muted-foreground">—</span>` when null; wrap badge in shadcn `Tooltip` showing short commit SHA (first 7 chars), `commitDate` formatted as locale date, and `latestTagSetBy.email` (or "Unknown user" when `latestTagSetBy` is null); add amber dot indicator (`<span className="h-2 w-2 rounded-full bg-amber-400" />`) adjacent to the "—" placeholder for rows with no pinned tag
- [ ] T025 [P] [US2] Write Vitest component test for the "Latest tag" column in `frontend/tests/unit/ProjectRepositoriesTable.test.tsx` — cover: pinned tag renders badge with correct name; tooltip shows SHA + date + email; null tag shows "—"; null tag shows amber dot; null `latestTagSetBy` falls back to "Unknown user" in tooltip

**Checkpoint**: User Story 2 is independently functional. Project screen shows tag column for all authenticated users without any admin action beyond the initial pin.

---

## Phase 5: User Story 3 — Tag List Reflects Current Remote State (Priority: P3)

**Goal**: The "Fetch tags" flow shows a loading state, surfaces provider errors with a Retry button, and warns the Admin when the currently pinned tag is no longer present in the live list.

**Independent Test**: Fetch tags with a simulated network error → error toast with Retry appears, no stale list shown. Fetch tags when pinned tag has been deleted from remote → warning banner appears above the tag list. Panel open alone triggers no provider call.

### Implementation for User Story 3

- [ ] T026 [US3] Add loading state and error handling to `TagPickerDialog.tsx` in `frontend/src/features/repositories/components/TagPickerDialog.tsx` — show `Skeleton` rows while `['repository', id, 'tags']` query is loading; on query error display a toast with the provider error message and a "Retry" button that calls `refetch()`; disable all table controls while loading
- [ ] T027 [US3] Add stale-pinned-tag warning banner to `RepositoryDetailSheet.tsx` in `frontend/src/features/repositories/components/RepositoryDetailSheet.tsx` — after tags are fetched, compare the fetched tag list against the currently pinned `repository.latestTag`; if pinned tag name is absent from the fetched list show a shadcn `Alert` (variant warning): "The pinned tag is no longer present in the remote repository. Please select a new one." — banner is only shown post-fetch, never on panel open alone (FR-011)
- [ ] T028 [P] [US3] Write WireMock integration tests for `AzureDevOpsGitProvider.ListTagsAsync` in `backend/tests/RepoManager.IntegrationTests/Infrastructure/AzureDevOpsListTagsTests.cs` — cover: empty tag list returns empty collection; annotated tags use `peeledObjectId`; lightweight tags fall back to `objectId`; HTTP 401 from provider throws `ExternalServiceException`; network timeout throws `ExternalServiceException`
- [ ] T029 [P] [US3] Write Playwright E2E test for the full Admin pin-tag flow in `frontend/tests/e2e/repositoryLatestTag.spec.ts` — Admin logs in → Settings → Repositories → clicks a tracked repo → sheet opens → Fetch tags → list appears → selects a tag → Confirm → project screen shows badge → Clear → project screen shows "—" with amber dot

**Checkpoint**: All three user stories are independently functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Observability, build validation, and final smoke test.

- [ ] T030 [P] Add `repository.latest_tag.changed` structured log entry (old value → new value, `repositoryId`, `actingUserId`) to `RepositoryService.SetLatestTagAsync` and `RepositoryService.ClearLatestTagAsync` in `backend/src/RepoManager.Infrastructure/Repositories/RepositoryService.cs` — confirm `repository.tags.fetched` log (duration, tag count, outcome) is already emitted in T009's `AzureDevOpsGitProvider.ListTagsAsync`
- [ ] T031 Run full build validation — `dotnet build backend/src` (zero warnings as errors) + `npm run lint && npm run build` from `frontend/` — confirm no compile errors introduced by this feature
- [ ] T032 Execute quickstart.md smoke test end-to-end: apply migration → start backend → start frontend → complete all 8 steps in `quickstart.md §Step 6: Verify End-to-End` → confirm Admin and Viewer flows both work as specified

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **blocks all user story phases**
- **Phase 3 (US1)**: Depends on Phase 2 — T009 → T010 → T012 → T013 → T014 (sequential within service); T015/T016/T017 [P] after T014; T021/T022 [P] after T019
- **Phase 4 (US2)**: Depends on Phase 2 and T019 (frontend codegen) — can start in parallel with Phase 3 back-end work once codegen is done
- **Phase 5 (US3)**: Depends on T021 and T022 (components must exist before enhancing them)
- **Phase 6 (Polish)**: Depends on all prior phases complete

### User Story Dependencies

- **US1 (P1)**: Depends on Phase 2 only — no dependencies on US2 or US3
- **US2 (P2)**: Depends on Phase 2 + T019 (codegen) only — the project screen column reads from the same `RepositoryDto` fields already added in Phase 2
- **US3 (P3)**: Depends on T021 and T022 from US1 — loading/error states and the warning banner are enhancements to components built in US1

### Within Phase 3

```
T009 → T010 → T012 → T013 → T014   (service layer, sequential — same file)
T011 [P]                             (DTO + validator, separate file)
T015, T016, T017 [P]                (controller actions, separate methods, after T014)
T018 [P]                            (integration tests, after T015-T017)
T019 → T020 → T021 [P], T022 [P]   (frontend, after codegen)
T023                                (wires sheet into page, after T022)
```

### Parallel Opportunities

- T002, T003, T005, T006, T007 can all start in parallel (different files)
- T011, T015, T016, T017, T018 can run in parallel once T014 is complete
- T021 and T022 can run in parallel (different component files)
- T024 and T025 can run in parallel with Phase 3 back-end tasks once T007 is done
- T028 and T029 can run in parallel with T026 and T027

---

## Parallel Example: Phase 2

```
Start in parallel:
  T002: Create RepositoryTag.cs value object
  T003: Write failing domain unit tests (TDD Red)
  T005: Add ListTagsAsync to IGitProviderService
  T006: Add GetTagsAsync/SetLatestTagAsync/ClearLatestTagAsync to IRepositoryService
  T007: Extend RepositoryDto + add UserSummaryDto

Then sequentially:
  T004: Implement domain methods (makes T003 green)
  T008: Update RepositoryConfiguration.cs
```

## Parallel Example: Phase 3 (Backend + Frontend split)

```
Backend stream:
  T009 → T010 → T012 → T013 → T014 → T015/T016/T017 [P] → T018

Frontend stream (starts after T019):
  T019 → T020 → T021 [P] / T022 [P] → T023
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Apply migration
2. Complete Phase 2: Domain + interfaces + EF config (blocks everything)
3. Complete Phase 3: Full Admin pin/clear flow — backend + frontend
4. **STOP and VALIDATE**: Manual smoke test, run T018 integration tests
5. Demo or ship US1 independently

### Incremental Delivery

1. **Phase 1 + 2** → Foundation ready
2. **Phase 3** → Admin can pin/clear tags (MVP)
3. **Phase 4** → Project screen shows pinned tags to all users
4. **Phase 5** → Tag list error handling + stale-tag warning
5. **Phase 6** → Observability + build sign-off

---

## Notes

- `[P]` tasks modify different files and have no unmet dependencies — safe to run concurrently
- `[USN]` label maps each task to a specific user story for traceability
- TDD (T003 before T004) is **mandatory** per Constitution Principle III — do not skip
- Commit after each checkpoint (end of each Phase) at minimum
- If a checkpoint fails, diagnose before advancing to the next phase
