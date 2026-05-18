---

description: "Task list for Per-Repo Jira Coverage feature implementation"
---

# Tasks: Per-Repo Jira Coverage

**Input**: Design documents from `specs/004-repo-jira-coverage/`
**Branch**: `004-repo-jira-coverage`
**Prerequisites**: plan.md ✅ spec.md ✅ research.md ✅ data-model.md ✅ contracts/ ✅ quickstart.md ✅

**Tests**: SemVer domain unit tests and service integration tests are included because the constitution requires TDD for all domain logic (Principle III).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1–US4 maps to spec.md priorities P1–P4)

---

## Phase 1: Setup (Domain + Application Layer Foundations)

**Purpose**: Domain value objects, entities, enums, DTOs, and service interfaces. All story phases depend on this phase.

**⚠️ CRITICAL**: Complete T001–T010 before any user story work begins.

- [X] T001 Write failing unit tests for `SemVer.TryParse` and `NextMinor` covering 7 cases from research.md Decision 2 in `backend/tests/RepoManager.UnitTests/Domain/SemVerTests.cs` *(TDD — tests MUST fail before T002)*
- [X] T002 Implement `SemVer` sealed record (`TryParse`, `NextMinor`, `ToString`) in `backend/src/RepoManager.Domain/ValueObjects/SemVer.cs` — confirm all 7 tests from T001 pass
- [X] T003 [P] Create `HealthBand` enum (`Green`, `Amber`, `Red`, `Unknown`) in `backend/src/RepoManager.Domain/Enums/HealthBand.cs`
- [X] T004 [P] Create `RepoJiraComparisonSnapshot` entity class with all columns from data-model.md §1 in `backend/src/RepoManager.Domain/Entities/RepoJiraComparisonSnapshot.cs`
- [X] T005 [P] Add `LastViewedAt` nullable `DateTime?` property and `JiraComparisonSnapshots` nav prop (`ICollection<RepoJiraComparisonSnapshot>`) to `Repository` entity in `backend/src/RepoManager.Domain/Entities/Repository.cs`
- [X] T006 [P] Create `RepoJiraComparisonDto` and `ComparisonCounts` DTO records per data-model.md §5 in `backend/src/RepoManager.Application/Jira/Dtos/RepoJiraComparisonDto.cs` and `ComparisonCounts.cs`
- [X] T007 [P] Create `TicketSummaryDto`, `CommitSummaryDto`, and `JiraIssueSummary` DTO records in `backend/src/RepoManager.Application/Jira/Dtos/` (three files)
- [X] T008 [P] Create `ProjectJiraCoverageDto` and `AddToFixVersionResultDto` DTO records in `backend/src/RepoManager.Application/Jira/Dtos/ProjectJiraCoverageDto.cs` and `AddToFixVersionResultDto.cs`
- [X] T009 [P] Create `IRepoJiraComparisonService` interface (`GetForRepoAsync`, `GetForProjectAsync`, `AddTicketToFixVersionAsync`) per contracts/service-interfaces.md in `backend/src/RepoManager.Application/Jira/IRepoJiraComparisonService.cs`
- [X] T010 [P] Add `GetTicketsInFixVersionAsync`, `AddTicketToFixVersionAsync`, and `CreateFixVersionAsync` signatures to `IJiraService` interface per contracts/service-interfaces.md in `backend/src/RepoManager.Application/Jira/IJiraService.cs`

**Checkpoint**: Domain layer compiles, SemVer tests pass, all interfaces defined. Foundational phase can now begin.

---

## Phase 2: Foundational Infrastructure

**Purpose**: EF persistence configuration, migration, and IJiraService implementations. Must complete before any user story work.

**⚠️ CRITICAL**: T014 (migration) requires T011 and T012 to complete first.

- [X] T011 [P] Create `RepoJiraComparisonSnapshotConfiguration` EF Fluent API config (unique index on `(RepositoryId, NextVersion)`, cascade delete, decimal precision, JSON column defaults) in `backend/src/RepoManager.Infrastructure/Persistence/Configurations/RepoJiraComparisonSnapshotConfiguration.cs`
- [X] T012 [P] Add `LastViewedAt` column mapping and `HasMany(r => r.JiraComparisonSnapshots).WithOne(s => s.Repository).OnDelete(DeleteBehavior.Cascade)` to `RepositoryConfiguration` in `backend/src/RepoManager.Infrastructure/Persistence/Configurations/RepositoryConfiguration.cs`
- [X] T013 [P] Implement the three new `IJiraService` methods in `JiraService.cs`: `GetTicketsInFixVersionAsync` (JQL search), `AddTicketToFixVersionAsync` (PUT issue fixVersions update), `CreateFixVersionAsync` (POST /rest/api/3/version) — all using the existing Polly-wrapped Jira `HttpClient` in `backend/src/RepoManager.Infrastructure/Jira/JiraService.cs`
- [X] T014 Add `RepoJiraComparisonSnapshotConfiguration` to `AppDbContext.OnModelCreating` and run `dotnet ef migrations add AddRepoJiraComparisonSnapshot` then `dotnet ef database update` (depends on T011, T012) in `backend/src/RepoManager.Infrastructure/Persistence/AppDbContext.cs`

**Checkpoint**: Migration applied, `RepoJiraComparisonSnapshots` table exists, all IJiraService methods implemented. User story phases can now begin.

---

## Phase 3: User Story 1 — Project Page Coverage Overview (Priority: P1) 🎯 MVP

**Goal**: Project detail page shows per-repo Jira coverage cards sorted worst-first with an aggregate header; cold-cache repos show skeleton cards that hydrate via individual per-repo fetches.

**Independent Test**: Navigate to a project with ≥ 2 repositories; verify coverage cards appear sorted by match rate ascending, aggregate header shows correct counts, and a cold-cache repo shows a Skeleton card that resolves.

### Implementation for User Story 1

- [X] T015 [US1] Implement `RepoJiraComparisonService.GetForRepoAsync` — cache read (return snapshot if age < 5 min and `forceRefresh=false`), SemVer parse (return Unsupported DTO if non-semver), Git commits-since-tag fetch, Jira fix-version ticket fetch, set-difference bucket computation, `MatchRate` + `HealthBand` calculation, snapshot upsert, `LastViewedAt` update in `backend/src/RepoManager.Infrastructure/Jira/RepoJiraComparisonService.cs`
- [X] T016 [US1] Implement `RepoJiraComparisonService.GetForProjectAsync` — iterate all repos assigned to the project, call `GetForRepoAsync` per repo sequentially, return cold repos as sentinel DTOs (`Supported=false`, `Health=Unknown`, empty buckets) without triggering external API calls; compute weighted project match rate only over supported repos in `backend/src/RepoManager.Infrastructure/Jira/RepoJiraComparisonService.cs` *(depends on T015)*
- [X] T017 [P] [US1] Add `GET /repositories/{id}/jira-coverage?refresh=` action to `JiraCoverageController` — returns `RepoJiraComparisonDto`, maps `NotFoundException` to 404 in `backend/src/RepoManager.Api/Controllers/JiraCoverageController.cs` *(depends on T015)*
- [X] T018 [P] [US1] Add `GET /projects/{id}/jira-coverage?refresh=` action to `JiraCoverageController` — returns `ProjectJiraCoverageDto`, repos sorted matchRate asc (unsupported at end) in `backend/src/RepoManager.Api/Controllers/JiraCoverageController.cs` *(depends on T016)*
- [X] T019 [P] [US1] Add cache invalidation after commit sync: after `CommitSyncService` saves new commits for a repository, set `LastSyncedAt = DateTime.MinValue` on all `RepoJiraComparisonSnapshots` rows for that `RepositoryId` in `backend/src/RepoManager.Infrastructure/Sync/CommitSyncService.cs`
- [X] T020 [P] [US1] Add cache invalidation after tag change: after `RepositoryService.SetLatestTagAsync` persists a new tag, set `LastSyncedAt = DateTime.MinValue` on all `RepoJiraComparisonSnapshots` for that `RepositoryId` in `backend/src/RepoManager.Infrastructure/Repositories/RepositoryService.cs`
- [X] T021 [US1] Register `IRepoJiraComparisonService → RepoJiraComparisonService` as scoped in DI, then regenerate frontend API types: `npm run codegen` from `frontend/` in `backend/src/RepoManager.Api/Program.cs` *(depends on T017, T018)*
- [X] T022 [P] [US1] Create `HealthPill.tsx` — shadcn `Badge` coloured by `HealthBand` (green=`bg-green-500`, amber=`bg-amber-500`, red=`bg-red-500`, unknown=`bg-gray-400`); props: `{ matchRate: number; health: string }` in `frontend/src/features/jira-coverage/components/HealthPill.tsx`
- [X] T023 [P] [US1] Create `BucketList.tsx` — shadcn `Collapsible` three-bucket renderer accepting `inBoth`, `jiraOnly`, `gitOnly` arrays; "In both" collapsed by default when ≥ 5 items, others expanded; each row shows ticket key (linked), summary, status `Badge`, assignee avatar, commit count in `frontend/src/features/jira-coverage/components/BucketList.tsx`
- [X] T024 [P] [US1] Create `useJiraCoverage.ts` — TanStack Query hooks: `useProjectCoverage(projectId, refresh?)`, `useRepoCoverage(repoId, refresh?)` calling generated API client; `invalidateRepoCoverage` and `invalidateProjectCoverage` helpers for post-mutation cache busting in `frontend/src/features/jira-coverage/hooks/useJiraCoverage.ts` *(depends on T021 for generated types)*
- [X] T025 [US1] Create `RepoCoverageCard.tsx` — shadcn `Card` showing: repo name + current tag, fix version link (clickable to Jira when exists), three counter badges (commits/git tickets/Jira tickets), `HealthPill`, three-bucket count strip (in-both/Jira-only/git-only counts), "View details" link, re-sync icon `Button` with last-synced `Tooltip` in `frontend/src/features/jira-coverage/components/RepoCoverageCard.tsx` *(depends on T022, T024)*
- [X] T026 [US1] Create `ProjectCoverageAggregate.tsx` — summary strip: total repos, green count, attention (amber+red) count, project-wide weighted match rate with `HealthPill`; props accept `ProjectJiraCoverageDto` in `frontend/src/features/jira-coverage/components/ProjectCoverageAggregate.tsx` *(depends on T022, T024)*
- [X] T027 [US1] Wire Jira coverage into `ProjectDetailPage.tsx` — call `useProjectCoverage`, render `ProjectCoverageAggregate` above the card grid, sort cards by `matchRate` ascending with a toggle to alphabetical; cold repos (sentinel `health: Unknown`) render shadcn `Skeleton` card and fire individual `useRepoCoverage` calls to hydrate in `frontend/src/features/projects/pages/ProjectDetailPage.tsx` *(depends on T025, T026)*

**Checkpoint**: Project page shows coverage cards and aggregate header; cold-cache repos show skeletons that resolve. User Story 1 independently testable.

---

## Phase 4: User Story 2 — Repository Page Jira Coverage Tab (Priority: P2)

**Goal**: Repository detail page gains a "Jira coverage" tab with the full three-bucket breakdown, four summary cards, header strip, and unmatched commits panel. Visible to both Admin and Viewer roles.

**Independent Test**: Navigate to a repository detail page, open the "Jira coverage" tab; verify four summary cards, all three buckets with correct ticket counts, and the unmatched commits panel are present.

### Implementation for User Story 2

- [ ] T028 [US2] Create `RepoCoverageTab.tsx` — header strip (repo name, tag, fix version, `HealthPill`, Re-sync `Button` visible to Admin only, last-synced timestamp visible to all), four summary `Card` components (commits/git tickets/Jira tickets/match rate), full `BucketList` (all three buckets), collapsed `Collapsible` unmatched commits panel listing SHA + author + message per `CommitSummaryDto` in `frontend/src/features/jira-coverage/components/RepoCoverageTab.tsx` *(depends on T023, T024)*
- [ ] T029 [US2] Add "Jira coverage" `TabsTrigger` and `TabsContent` to `RepositoryDetailPage.tsx` rendering `<RepoCoverageTab repositoryId={id} />` in `frontend/src/features/repositories/pages/RepositoryDetailPage.tsx` *(depends on T028)*

**Checkpoint**: Repository detail page has a functional "Jira coverage" tab. User Stories 1 and 2 independently testable.

---

## Phase 5: User Story 3 — Add Git-Only Ticket to Jira Fix Version (Priority: P3)

**Goal**: Admin can click "Add to fix version" on any Git-only ticket, confirm via dialog, and the ticket moves to the "In both" bucket. Fix version is created in the ticket's own Jira project if it doesn't exist.

**Independent Test**: With a Git-only ticket visible in the bucket, click "Add to fix version", confirm the `AlertDialog`, verify the ticket moves to "In both" and match rate increases.

### Implementation for User Story 3

- [ ] T030 [US3] Implement `RepoJiraComparisonService.AddTicketToFixVersionAsync` — derive Jira project key from ticket key prefix (e.g., `PROJ-111` → `PROJ`), call `IJiraService.CreateFixVersionAsync(projectKey, fixVersionName, ct)` if fix version does not exist, call `IJiraService.AddTicketToFixVersionAsync(ticketKey, fixVersionName, ct)`, then set `LastSyncedAt = DateTime.MinValue` on the repo's snapshot; return `AddToFixVersionResultDto` in `backend/src/RepoManager.Infrastructure/Jira/RepoJiraComparisonService.cs` *(depends on T013)*
- [ ] T031 [US3] Add `POST /repositories/{id}/jira-coverage/add-ticket` action to `JiraCoverageController` with `[Authorize(Roles = "Admin")]`; maps `ConflictException` (non-semver tag) to 409, `ValidationException` (ticket not found) to 422 in `backend/src/RepoManager.Api/Controllers/JiraCoverageController.cs` *(depends on T030)*
- [ ] T032 [US3] Regenerate frontend API client after add-ticket endpoint added: `npm run codegen` from `frontend/` *(depends on T031)*
- [ ] T033 [US3] Add `useAddTicketToFixVersion` mutation to `useJiraCoverage.ts` — calls POST endpoint, on success invalidates `useRepoCoverage` and `useProjectCoverage` queries for the affected repo in `frontend/src/features/jira-coverage/hooks/useJiraCoverage.ts` *(depends on T032)*
- [ ] T034 [US3] Add "Add to fix version" `Button` (Admin role only — hide for Viewer) to Git-only bucket rows in `BucketList.tsx`; clicking opens a shadcn `AlertDialog` stating the fix version name; confirming calls `useAddTicketToFixVersion`; button shows loading state while mutation is in flight in `frontend/src/features/jira-coverage/components/BucketList.tsx` *(depends on T033)*

**Checkpoint**: Admin can add Git-only tickets to the Jira fix version from within the app. User Story 3 independently testable.

---

## Phase 6: User Story 4 — Force Re-sync (Priority: P4)

**Goal**: Admin can force a coverage refresh at any time via the Re-sync button on both the project page cards and the repository coverage tab; the button shows a loading state and last-synced timestamp updates after success.

**Independent Test**: With stale cached data, click Re-sync, verify loading state on button, data updates, and lastSyncedAt timestamp changes.

### Implementation for User Story 4

- [ ] T035 [P] [US4] Wire Re-sync icon `Button` in `RepoCoverageCard.tsx` — clicking calls `useRepoCoverage(repoId, refresh=true)` as a refetch; button shows spinner while loading; Tooltip shows `lastSyncedAt` formatted as relative time; visible to Admin only (hide for Viewer) in `frontend/src/features/jira-coverage/components/RepoCoverageCard.tsx` *(depends on T025)*
- [ ] T036 [P] [US4] Wire Re-sync `Button` in `RepoCoverageTab.tsx` header — same behaviour as T035; last-synced timestamp visible to all roles in `frontend/src/features/jira-coverage/components/RepoCoverageTab.tsx` *(depends on T028)*

**Checkpoint**: All four user stories are complete and independently testable end-to-end.

---

## Phase 7: Background Refresh Service

**Purpose**: Keeps project-page caches warm for recently-viewed repositories so cold-cache skeleton states are rare in practice.

- [ ] T037 Create `JiraCoverageRefreshService` extending `BackgroundService` using `PeriodicTimer` (10-minute interval); on each tick query `Repository` rows where `LastViewedAt > now - 24h` and their snapshot `LastSyncedAt < now - 5min`; call `_comparisonService.GetForRepoAsync(id, forceRefresh: true, ct)` per result; log `jira_coverage.background_refresh` event in `backend/src/RepoManager.Infrastructure/BackgroundServices/JiraCoverageRefreshService.cs`
- [ ] T038 Register `JiraCoverageRefreshService` as hosted service via `builder.Services.AddHostedService<JiraCoverageRefreshService>()` in `backend/src/RepoManager.Api/Program.cs` *(depends on T037)*

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Test coverage required by the constitution, and end-to-end smoke validation.

- [ ] T039 [P] Write integration tests for `JiraCoverageController` endpoints: cache hit (no external calls), cache miss (WireMock for Jira), Admin vs Viewer RBAC (403 for Viewer on add-ticket), 404 for unknown repo/project, 409 for non-semver tag in `backend/tests/RepoManager.IntegrationTests/Api/JiraCoverageTests.cs`
- [ ] T040 [P] Write integration tests for `RepoJiraComparisonService`: snapshot upsert idempotency, forceRefresh bypasses TTL, add-ticket creates fix version on demand, invalidation sets `LastSyncedAt = MinValue` — all against real SQLite temp file + WireMock for Jira in `backend/tests/RepoManager.IntegrationTests/Infrastructure/RepoJiraComparisonServiceTests.cs`
- [ ] T041 Manual end-to-end smoke test per `quickstart.md` Step 10: project page cards sorted worst-first, cold skeleton hydration, re-sync updates timestamp, add-to-fix-version moves ticket, Viewer role restrictions, non-semver warning state

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately; T003–T010 all parallelisable after T001 completes
- **Phase 2 (Foundational)**: Depends on Phase 1 completion; T011–T013 parallelisable; T014 depends on T011+T012
- **Phase 3 (US1)**: Depends on Phase 2 — T015 first, T016 after T015, T017+T018+T019+T020 after T015/T016; T022–T024 parallelisable (frontend)
- **Phase 4 (US2)**: Depends on T023+T024 (BucketList + hook)
- **Phase 5 (US3)**: Depends on T013 (IJiraService impl), then T030→T031→T032→T033→T034
- **Phase 6 (US4)**: Depends on T025 (RepoCoverageCard) and T028 (RepoCoverageTab)
- **Phase 7 (Background)**: Depends on Phase 2 (service + DI) — can implement after US1
- **Phase 8 (Polish)**: Depends on all desired user stories complete

### User Story Dependencies

- **US1 (P1)**: Depends on Phase 2 complete — no dependency on other stories
- **US2 (P2)**: Depends on US1 frontend (T023 BucketList, T024 hook)
- **US3 (P3)**: Depends on Phase 2 (IJiraService impl T013) — backend independent of US1/US2; frontend depends on T033 mutation which depends on T021 codegen (US1)
- **US4 (P4)**: Depends on US1 (T025) and US2 (T028) components

### Within Each Phase

- Domain tasks (T001–T010) must precede Infrastructure tasks (T011–T014)
- SemVer tests (T001) must precede implementation (T002)
- Service implementation (T015) must precede controller (T017)
- Backend endpoints must be live before frontend codegen (T021, T032)
- Hooks (T022, T023, T024) must precede components that use them (T025, T026)

### Parallel Opportunities

- T003–T010: All parallelisable (different files, no dependencies after T001)
- T011, T012, T013: All parallelisable (different files)
- T019, T020: Parallelisable (different service files)
- T022, T023, T024: All parallelisable (different frontend files)
- T035, T036: Parallelisable (different components)
- T039, T040: Parallelisable (different test files)

---

## Parallel Example: Phase 1 Setup

```
# After T001+T002 complete, launch in parallel:
T003 — HealthBand enum
T004 — RepoJiraComparisonSnapshot entity
T005 — Repository.LastViewedAt
T006 — RepoJiraComparisonDto + ComparisonCounts
T007 — TicketSummaryDto + CommitSummaryDto + JiraIssueSummary
T008 — ProjectJiraCoverageDto + AddToFixVersionResultDto
T009 — IRepoJiraComparisonService interface
T010 — IJiraService extensions
```

## Parallel Example: User Story 1 Frontend

```
# After T021 (codegen) completes, launch in parallel:
T022 — HealthPill.tsx
T023 — BucketList.tsx
T024 — useJiraCoverage.ts
# Then: T025 (depends T022+T024), T026 (depends T024)
# Then: T027 (depends T025+T026)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (TDD SemVer → domain + DTOs + interfaces)
2. Complete Phase 2: Foundational (EF config + migration + IJiraService impl)
3. Complete Phase 3: User Story 1 (service → endpoints → frontend cards)
4. **STOP and VALIDATE**: Project page shows coverage cards with correct data
5. Demo / validate against SC-001, SC-003, SC-005, SC-006

### Incremental Delivery

1. Phase 1 + 2 → Foundation ready
2. Phase 3 → Project page with coverage cards (MVP)
3. Phase 4 → Repository coverage tab added
4. Phase 5 → Add-to-fix-version action enabled
5. Phase 6 → Re-sync button wired
6. Phase 7 → Background refresh keeps caches warm

### Parallel Team Strategy

With two developers after Phase 2 completes:
- **Developer A**: T015–T020 (backend service + endpoints + cache invalidation) then T037–T038 (background service)
- **Developer B**: T022–T024 (frontend HealthPill, BucketList, hook) then T025–T027 (cards + project page wiring)

---

## Notes

- [P] tasks have no dependencies on incomplete tasks in the same phase — safe to parallelise
- [Story] label maps each task to its user story for traceability
- SemVer tests (T001) **must fail** before T002 is implemented — Red-Green-Refactor
- Regenerate `frontend/src/lib/api.d.ts` via `npm run codegen` after any backend endpoint changes (T021, T032)
- Each story phase is a complete, independently testable increment
- Stop at any phase checkpoint to validate the story before continuing
