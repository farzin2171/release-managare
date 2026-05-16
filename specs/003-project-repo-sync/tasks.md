# Tasks: Project Screen — Repository Sync & Changes Persistence

**Input**: Design documents from `specs/003-project-repo-sync/`
**Prerequisites**: plan.md ✅ | spec.md ✅ | research.md ✅ | data-model.md ✅ | contracts/ ✅

**Tests**: Included — constitution requires TDD (red-first) for all domain logic; integration tests against real SQLite; Playwright E2E for the end-to-end sync flow.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to — [US1] Single repo sync, [US2] Project-wide sync, [US3] Persisted state on load, [US4] Contributor visibility
- Each file path is relative to the repository root

---

## Phase 1: Setup

**Purpose**: Verify no naming conflicts and establish the EF context extension points for the two new tables before any implementation begins.

- [ ] T001 Review `RepoManagerDbContext` for conflicts; confirm `RepositorySyncs` and `ProjectSyncs` are not already defined; note the two `DbSet` properties to add in T010

**Checkpoint**: T001 complete — safe to begin parallel foundational work

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain aggregates, persistence schema, and shared application types that ALL user stories depend on. Must be complete before any user story work starts.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete. Domain tests MUST be written first and confirmed failing (RED) before aggregates are implemented.

### Domain — Tests First (RED)

- [ ] T002 [P] Write `RepositorySyncStateTests` (RED — all state transitions: Pending→InProgress, InProgress→Succeeded/Failed/Skipped, illegal transitions throw, SetStep only valid while InProgress, Complete idempotency throws) in `backend/tests/RepoManager.Domain.Tests/RepositorySyncStateTests.cs`
- [ ] T003 [P] Write `ProjectSyncStateTests` (RED — all transitions: Pending→InProgress, InProgress→Succeeded/PartiallyFailed/Failed/Cancelled, RecordChildResult updates counts correctly, illegal transitions throw) in `backend/tests/RepoManager.Domain.Tests/ProjectSyncStateTests.cs`

### Domain — Implementation (make T002/T003 GREEN)

- [ ] T004 [P] Create `SyncStatus`, `ProjectSyncStatus` enums and `SyncStep` string constants in `backend/src/RepoManager.Domain/Enums/SyncStatus.cs`, `ProjectSyncStatus.cs`, `SyncStep.cs`
- [ ] T005 [P] Create `ContributorSnapshot` record (`Name`, `Email`, `Commits`) in `backend/src/RepoManager.Domain/ValueObjects/ContributorSnapshot.cs`
- [ ] T006 Implement `RepositorySync` aggregate with `Start()`, `Skip(reason)`, `SetStep(step)`, `Complete(commitCount, ticketCount, breakingCount, contributors)`, `Fail(message)` state-transition methods in `backend/src/RepoManager.Domain/Aggregates/RepositorySync.cs` — confirm T002 goes GREEN
- [ ] T007 Implement `ProjectSync` aggregate with `Start()`, `RecordChildResult(status)`, `Complete()`, `Cancel()` methods and final-status computation logic in `backend/src/RepoManager.Domain/Aggregates/ProjectSync.cs` — confirm T003 goes GREEN

### Persistence

- [ ] T008 [P] Create `RepositorySyncConfiguration` with all column mappings, FK constraints, and composite index `(RepositoryId, FromTag, Status, StartedAt DESC)` in `backend/src/RepoManager.Infrastructure/Persistence/Configurations/RepositorySyncConfiguration.cs`
- [ ] T009 [P] Create `ProjectSyncConfiguration` with column mappings, FK constraints, and unique partial index on `ProjectId` where `Status IN (0, 1)` in `backend/src/RepoManager.Infrastructure/Persistence/Configurations/ProjectSyncConfiguration.cs`
- [ ] T010 Add `DbSet<RepositorySync>` and `DbSet<ProjectSync>` to `RepoManagerDbContext`; register both new configurations via `modelBuilder.ApplyConfiguration(...)` in `backend/src/RepoManager.Infrastructure/Persistence/RepoManagerDbContext.cs`
- [ ] T011 Generate migration `AddSyncTables` via `dotnet ef migrations add AddSyncTables --project backend/src/RepoManager.Infrastructure --startup-project backend/src/RepoManager.Api`; apply to dev DB; verify both tables and the partial index exist in `sqlite_master`

### Application Layer Shared Types

- [ ] T012 [P] Create `RepositorySyncDto`, `ProjectSyncDto`, `RepoSyncSnapshotItemDto` record types with Mapster configuration in `backend/src/RepoManager.Application/DTOs/`
- [ ] T013 [P] Create `ISyncJobQueue`, `SyncJob` in `backend/src/RepoManager.Application/Queues/` and `ISyncEventPublisher`, `SyncEvent` (`Type`, `RepoId`, `RepoName`, `Status`, `CurrentStep`, `ElapsedMs`, `Counts`) in `backend/src/RepoManager.Application/Events/`
- [ ] T014 [P] Create stub interfaces `IRepositorySyncService`, `IProjectSyncService`, `IProjectSyncSnapshotService` with method signatures matching the contracts in `backend/src/RepoManager.Application/Services/`

**Checkpoint**: All domain tests GREEN; migration applied; interfaces defined — user story implementation can now begin

---

## Phase 3: User Story 1 — Single Repository Sync (Priority: P1) 🎯 MVP

**Goal**: A user clicks "Sync" on a repository card; the card shows live progress through five phases; on completion the four metrics update and persist; a failed sync shows the error and a Retry button.

**Independent Test**: With at least one repository that has a pinned tag, click "Sync", verify the card transitions through states, metrics update, and on a second sync the DB is updated in-place (no duplicates). Test the no-pinned-tag case (button disabled + Skipped row).

### Backend Infrastructure

- [ ] T015 [P] Implement `InMemorySyncJobQueue` (bounded `Channel<SyncJob>`, capacity 200) and register as `ISyncJobQueue` singleton in `backend/src/RepoManager.Infrastructure/Sync/InMemorySyncJobQueue.cs`
- [ ] T016 [P] Implement `InMemorySyncEventPublisher` (keyed `ConcurrentDictionary<Guid, Channel<SyncEvent>>`; 30-min TTL cleanup; bounded capacity 100 per channel) and register as `ISyncEventPublisher` singleton in `backend/src/RepoManager.Infrastructure/Sync/InMemorySyncEventPublisher.cs`
- [ ] T017 Implement `SyncBackgroundService` (`BackgroundService`): on `StartAsync` run stale-sync recovery (mark rows `InProgress` older than 30 min as `Failed` with `ErrorMessage = "Stale — worker restarted"`); in `ExecuteAsync` loop, dequeue jobs, resolve scoped `IServiceProvider`, dispatch; catch all exceptions and mark row `Failed` in `backend/src/RepoManager.Infrastructure/Sync/SyncBackgroundService.cs`

### Repository Sync Service

- [ ] T018 [US1] Implement `RepositorySyncService` enqueue path: validate repo has `LatestTag` (else create `Skipped` row); check no active sync for same repo (else throw `ConflictException`, FR-024); create `Pending` row; enqueue `SyncJob`; return DTO in `backend/src/RepoManager.Infrastructure/Sync/RepositorySyncService.cs`
- [ ] T019 [US1] Implement `RepositorySyncService` execute path (called by worker only): flip to `InProgress`; walk five steps via `SetStep`; fetch commits (`IGitProvider.ListCommitsAsync`); enforce 5,000-commit cap (fail with clear message if exceeded); parse via `IConventionalCommitParser`; upsert `Commits` rows; aggregate tickets; build `ContributorSnapshot` list (dedup by lowercased email, name fallback); wrap in transaction; call `Complete()`; publish `SyncEvent` per step; on any exception call `Fail()` in `backend/src/RepoManager.Infrastructure/Sync/RepositorySyncService.cs`

### API

- [ ] T020 [US1] Implement `RepositorySyncsController` with `[Authorize]`: `POST /api/v1/repositories/{id}/sync` → 202; `GET /api/v1/repositories/{id}/sync/latest` → 200/404; `GET /api/v1/repository-syncs/{syncId}` → 200/404 in `backend/src/RepoManager.Api/Controllers/RepositorySyncsController.cs`
- [ ] T021 [US1] Register `IRepositorySyncService` → `RepositorySyncService` (scoped) and `SyncBackgroundService` as `IHostedService` in `backend/src/RepoManager.Api/Program.cs`

### Backend Tests

- [ ] T022 [P] [US1] Integration test `SyncBackgroundServiceTests`: enqueue a noop sync job; assert worker picks it up within 2s; assert structured log line emitted; assert published event reaches a subscriber in `backend/tests/RepoManager.Infrastructure.Tests/SyncBackgroundServiceTests.cs`
- [ ] T023 [P] [US1] Integration test `RepositorySyncIntegrationTests`: (a) fake provider returns 50 commits → sync → assert counts, `Commits` rows, `Tickets` rows; (b) re-sync same (repo, tag) → assert idempotency (no duplicate rows, counts unchanged); (c) five `CurrentStep` events published in correct order; (d) concurrent sync request for same repo → 409 in `backend/tests/RepoManager.Api.Tests/RepositorySyncIntegrationTests.cs`

### Frontend

- [ ] T024 [P] [US1] Create `syncApi.ts` with typed wrappers for `POST /repositories/{id}/sync`, `GET /repositories/{id}/sync/latest`, `GET /repository-syncs/{syncId}` using the generated OpenAPI client in `frontend/src/lib/api/syncApi.ts`
- [ ] T025 [P] [US1] Create `useRepositorySync` hook: `useMutation` for trigger; `useQuery` with `refetchInterval: 2000` while status is `Pending` or `InProgress`; set `refetchInterval: false` on terminal state in `frontend/src/features/projects/hooks/useRepositorySync.ts`
- [ ] T026 [P] [US1] Create `RepoCardSyncOverlay` component with all five states (idle-never-synced, idle-synced, in-progress with step message, failed with error reason, no-pinned-tag); use CSS variables `--color-background-info`, `--color-border-info`, `--color-background-danger`, `--color-border-danger` — no hardcoded hex in `frontend/src/features/projects/components/RepoCardSyncOverlay.tsx`
- [ ] T027 [P] [US1] Create `RepoCardSyncFooter` component: left side shows relative "last synced" timestamp or "Not synced yet"; right side shows Sync/Retry/disabled button with appropriate icon; uses CSS variables for state colours in `frontend/src/features/projects/components/RepoCardSyncFooter.tsx`
- [ ] T028 [US1] Edit `RepositoryCard.tsx`: (1) replace top-right `→ HEAD` text with tag chip pill (`v2.4.1 → HEAD` or `No tag → HEAD` with amber dot when no tag); (2) wrap existing four-metric grid in `<RepoCardSyncOverlay>`; (3) append `<RepoCardSyncFooter />` after the overlay in `frontend/src/features/projects/components/RepositoryCard.tsx`

**Checkpoint**: User Story 1 fully functional — click Sync on a card with a pinned tag and see live progress → metrics → persistence. Retry on failure. Disabled on no-tag.

---

## Phase 4: User Story 2 — Project-Wide Sync (Priority: P2)

**Goal**: "Sync project" syncs all repositories sequentially with live SSE-driven progress on the strip and each card; repos without tags are auto-skipped; failures don't stop the run; "Cancel" completes the in-flight repo then stops; a second concurrent attempt is rejected.

**Independent Test**: Start a project sync with 3 repos (one with tag, one without, one with a forced failure via a mock provider); verify the strip shows live progress, the strip shows correct final summary, and the stat cards animate to updated totals on completion.

### Backend

- [ ] T029 [US2] Implement `ProjectSyncService`: `EnqueueAsync` (check no active run via unique partial index; catch `DbUpdateException` → `ConflictException` → 409; create `Pending` row; enqueue job); `CancelActiveAsync` (flip to `Cancelling` transient state); `ExecuteAsync` (iterate repos sequentially, call `IRepositorySyncService.ExecuteAsync` per repo, skip repos with no tag, observe `Cancelling` flag between repos, compute final `ProjectSyncStatus`); extract private helpers `ProcessRepoAsync` and `FinaliseRunAsync` to stay under 30 lines per method in `backend/src/RepoManager.Infrastructure/Sync/ProjectSyncService.cs`
- [ ] T030 [US2] Implement `ProjectSyncsController` with `[Authorize]`: `POST /api/v1/projects/{id}/sync` → 202/409; `DELETE /api/v1/projects/{id}/sync/active` → 200/404; `GET /api/v1/projects/{id}/sync/latest` → 200/404 (includes child syncs); `GET /api/v1/projects/{id}/sync/active` → 200/204; `GET /api/v1/projects/{id}/sync/active/stream` → `Results.ServerSentEvents` emitting `repo_started`, `step_changed`, `repo_completed`, `project_complete` events; re-emit last 50 events to reconnecting clients via `Last-Event-ID` in `backend/src/RepoManager.Api/Controllers/ProjectSyncsController.cs`
- [ ] T031 [US2] Register `IProjectSyncService` → `ProjectSyncService` (scoped) in `backend/src/RepoManager.Api/Program.cs`

### Backend Tests

- [ ] T032 [P] [US2] Integration test `ProjectSyncConcurrencyTests`: (a) enqueue with 3 fake repos (1 success, 1 forced-failure, 1 no-tag) → assert final status `PartiallyFailed`, `SucceededCount=1`, `FailedCount=1`, `SkippedCount=1`; (b) second concurrent enqueue → 409; (c) cancel after repo 1 completes → assert run status `Cancelled`, repos 2+3 not started in `backend/tests/RepoManager.Infrastructure.Tests/ProjectSyncConcurrencyTests.cs`
- [ ] T033 [P] [US2] Integration test `ProjectSyncIntegrationTests`: full SSE event sequence for a project sync (assert `repo_started`, `step_changed`, `repo_completed`, `project_complete` in correct order with correct payloads); cancel mid-run via `DELETE /active` endpoint in `backend/tests/RepoManager.Api.Tests/ProjectSyncIntegrationTests.cs`

### Frontend

- [ ] T034 [P] [US2] Create `syncSse.ts` EventSource wrapper: auto-reconnect on error; send `Last-Event-ID` header; expose typed event callbacks per event type; call `onComplete` and close stream when `project_complete` received in `frontend/src/lib/api/syncSse.ts`
- [ ] T035 [P] [US2] Create `useProjectSync` hook: `useMutation` for start/cancel; subscribe to SSE stream via `syncSse.ts`; fall back to polling `GET /active` at 3s if SSE drops twice consecutively; patch TanStack Query cache snapshot as each `repo_completed` event arrives (so cards update without a full refetch) in `frontend/src/features/projects/hooks/useProjectSync.ts`
- [ ] T036 [P] [US2] Create `ProjectSyncStrip` component with three modes: idle (shows "Project last synced" relative timestamp + summary + "View run" link + "Sync project" button; or "Never synced" if no prior run); running (spinner, "Syncing… X of N complete", "Cancel" button, light-blue background via `--color-background-info`); just-completed (auto-dismisses after 30s back to idle) in `frontend/src/features/projects/components/ProjectSyncStrip.tsx`
- [ ] T037 [P] [US2] Create `ProjectSyncRunDrawer` component: opened via "View run" link in strip; calls `GET /projects/{id}/sync/latest`; lists per-repo sync outcomes (name, status, counts, elapsed time, error if any) in `frontend/src/features/projects/components/ProjectSyncRunDrawer.tsx`
- [ ] T038 [US2] Edit `ProjectDetailPage.tsx`: insert `<ProjectSyncStrip projectId={id} />` in exactly one location — between the existing title row and the existing `<UnreleasedChangesSummary />` component in `frontend/src/features/projects/pages/ProjectDetailPage.tsx`

**Checkpoint**: User Story 2 fully functional — "Sync project" button starts run, strip shows live progress, cards transition in sequence, cancel works, concurrent attempt rejected.

---

## Phase 5: User Story 3 — Persisted State on Return Visit (Priority: P3)

**Goal**: Opening the project screen immediately shows all persisted metrics for every repository card from the database — no Git provider call required. The "Project last synced" strip reads from the last successful project run.

**Independent Test**: After syncing at least one repository, navigate away and return; confirm all cards show correct metrics immediately on load (DevTools network tab shows no Azure DevOps API call). Change a repo's pinned tag and confirm that card reverts to "Not synced yet".

### Backend

- [ ] T039 [US3] Implement `ProjectSyncSnapshotService`: execute a single SQL query using EF correlated subquery to fetch the latest successful `RepositorySync` per repo for each repo's current `LatestTag`; wrap result in 5s sliding `IMemoryCache` per `projectId`; expose a cache-invalidation method called by `RepositorySyncService` on sync completion in `backend/src/RepoManager.Infrastructure/Sync/ProjectSyncSnapshotService.cs`
- [ ] T040 [US3] Add snapshot endpoint `GET /api/v1/projects/{id}/repositories/sync-snapshot` to `ProjectSyncsController`; returns `IEnumerable<RepoSyncSnapshotItemDto>` (one item per assigned repo; `LatestSync` null when not yet synced against current tag; `CurrentStep` non-null when a sync is in-flight) in `backend/src/RepoManager.Api/Controllers/ProjectSyncsController.cs`

### Backend Tests

- [ ] T041 [P] [US3] Integration test: snapshot returns correct data after one repo synced; after tag change on that repo `LatestSync` becomes null; after project sync completes all repos show data; cache invalidates within 5s of any child sync completing in `backend/tests/RepoManager.Api.Tests/RepositorySyncIntegrationTests.cs` (extend existing file)

### Frontend

- [ ] T042 [P] [US3] Create `useProjectSyncSnapshot` hook: single `useQuery(['project', id, 'sync-snapshot'])` calling `GET /repositories/sync-snapshot`; `staleTime: 5000`; this is the single source of truth for all card metrics on screen load in `frontend/src/features/projects/hooks/useProjectSyncSnapshot.ts`
- [ ] T043 [US3] Update `ProjectDetailPage.tsx`: wire stat cards (`UnreleasedChangesSummary`) to derive totals from the `useProjectSyncSnapshot` result (sum across repos where `LatestSync.Status === 'Succeeded'`) instead of calling the Git provider; animate stat card numbers to new values when snapshot updates after a sync in `frontend/src/features/projects/pages/ProjectDetailPage.tsx`

**Checkpoint**: User Story 3 fully functional — page loads metrics from DB only; changing pinned tag resets card; stat cards aggregate from snapshot.

---

## Phase 6: User Story 4 — Contributor Visibility (Priority: P4)

**Goal**: Clicking the "contributors" metric number on a repository card opens a popover listing each contributor's display name and commit count. The top "CONTRIBUTORS" stat card shows the de-duplicated union across all synced repos.

**Independent Test**: After syncing a repo with multiple contributors, click the contributors number; verify a popover opens with names and commit counts. Where two repos share a contributor by email, verify the project-level CONTRIBUTORS total counts them once.

- [ ] T044 [P] [US4] Create `ContributorsPopover` component: triggered by clicking the contributors metric label (cursor pointer, hover underline); anchored popover (shadcn `Popover`) lists `name + commits` per contributor from the `RepositorySyncDto.contributors` array; project-level dedup logic: build a `Map` keyed by `email.toLowerCase() || name.toLowerCase()` across all snapshot repos to compute the total unique count in `frontend/src/features/projects/components/ContributorsPopover.tsx`
- [ ] T045 [US4] Wire `ContributorsPopover` into `RepoCardSyncOverlay`: when the card is in idle-synced state, make the "contributors" count a clickable element that opens `<ContributorsPopover contributors={latestSync.contributors} />`; update `UnreleasedChangesSummary` to pass de-duplicated contributor total derived from snapshot in `frontend/src/features/projects/components/RepoCardSyncOverlay.tsx`

**Checkpoint**: User Story 4 fully functional — contributors popover opens on click; project-level count is de-duplicated by email.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Observability, E2E coverage, visual regression, and documentation.

- [ ] T046 [P] Add structured log entries to `RepositorySyncService.ExecuteAsync`: log at Information on success (`{CorrelationId}`, `{RepoName}`, `{CommitCount}`, `{TicketCount}`, `{ElapsedMs}`, `outcome: Succeeded`); log at Warning on failure (include `{ErrorMessage}`) in `backend/src/RepoManager.Infrastructure/Sync/RepositorySyncService.cs`
- [ ] T047 [P] Wire metrics counters in `ProjectSyncService`: increment `sync.repository.completed`, `sync.repository.failed` per child result; increment `sync.project.completed` at project-run terminal state via the existing `/metrics` endpoint instrumentation pattern in `backend/src/RepoManager.Infrastructure/Sync/ProjectSyncService.cs`
- [ ] T048 [P] Add audit log entries to `ProjectSyncService`: structured log at Information for project sync start (user ID, project ID, total repo count), complete (final status, counts, elapsed), and cancel (user ID, repos completed vs. total) in `backend/src/RepoManager.Infrastructure/Sync/ProjectSyncService.cs`
- [ ] T049 Playwright E2E test: full Tech-lead end-to-end — (1) confirm repo has pinned tag; (2) click Sync; (3) assert card transitions through at least 2 step messages; (4) assert metrics update on completion; (5) reload page; (6) assert metrics still show (no Git provider call in network tab); (7) click "Sync project"; (8) assert strip transitions through running mode and card overlays update in sequence; (9) assert final summary in strip in `frontend/e2e/project-sync.spec.ts`
- [ ] T050 [P] Visual regression: capture and commit Playwright screenshot baselines for project screen in three states: (a) never-synced (all cards show zeros + "Not synced yet"); (b) partially-synced (some cards with data, some zeros); (c) all-synced (all cards with data, strip shows last-synced timestamp); assert existing elements are pixel-stable in `frontend/e2e/project-screen-baseline.spec.ts`
- [ ] T051 [P] Add README section "Repository Sync" documenting: what triggers a sync (manual only, v1); what data is persisted (commits, tickets, contributors, breaking changes); how metrics are read on page load (snapshot endpoint, no Git calls); how to recover a stuck sync (auto-recovery after 30 min on worker restart, or manual DB update for development) in `backend/README.md` or top-level `README.md`

**Checkpoint**: Full E2E Tech-lead pass green; visual regression baselines committed; observability complete.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — **BLOCKS all user story phases**
- **Phase 3 (US1)**: Depends on Phase 2 — first user story, enables MVP
- **Phase 4 (US2)**: Depends on Phase 2 + Phase 3 (uses `IRepositorySyncService.ExecuteAsync` internally)
- **Phase 5 (US3)**: Depends on Phase 2 + Phase 3 (needs sync data to read back)
- **Phase 6 (US4)**: Depends on Phase 3 (needs `contributors` data from sync)
- **Phase 7 (Polish)**: Depends on Phases 3–6 all complete

### User Story Dependencies

- **US1 (P1)**: No dependency on other stories — can start after foundational ✅
- **US2 (P2)**: Depends on US1 (`ProjectSyncService` delegates execution to `RepositorySyncService`)
- **US3 (P3)**: Depends on US1 (needs persisted sync records to read back)
- **US4 (P4)**: Depends on US1 (`contributors` data captured during repo sync)

### Within Each Phase

- Tests MUST be written RED before implementation for domain aggregates (T002, T003 before T006, T007)
- Domain enums/value objects (T004, T005) before aggregates (T006, T007)
- EF configurations (T008, T009) before DbContext update (T010) before migration (T011)
- Application types (T012, T013, T014) can run in parallel with domain work
- Backend service before controller (e.g., T018/T019 before T020)
- `generate-client` must be re-run after any new controller is added, before frontend hooks are written

---

## Parallel Opportunities

### Phase 2 (Foundational) parallel batch

```
Parallel A: T002 (RepositorySyncStateTests — RED)
Parallel B: T003 (ProjectSyncStateTests — RED)
Parallel C: T004 (enums + SyncStep)
Parallel D: T005 (ContributorSnapshot record)
Parallel E: T012 (DTOs)
Parallel F: T013 (queue/event types)
Parallel G: T014 (service interfaces)
Then sequentially: T006 → T007 → T008 → T009 → T010 → T011
```

### Phase 3 (US1) parallel batch (after T017 SyncBackgroundService)

```
Parallel A: T018 + T019 (RepositorySyncService — enqueue + execute)
Parallel B: T022 (SyncBackgroundServiceTests)
After T019:
  Parallel C: T020 (controller) + T021 (DI)
  Parallel D: T023 (RepositorySyncIntegrationTests)
  Parallel E: T024 (syncApi.ts) + T025 (useRepositorySync)
  Parallel F: T026 (RepoCardSyncOverlay) + T027 (RepoCardSyncFooter)
Then: T028 (RepositoryCard edits — depends on T026, T027)
```

### Phase 4 (US2) parallel batch

```
Parallel A: T029 (ProjectSyncService) + T034 (syncSse.ts)
After T029: Parallel B: T030 (controller) + T031 (DI)
After T030: Parallel C: T032 + T033 (backend tests) + T035 (useProjectSync)
            Parallel D: T036 (ProjectSyncStrip) + T037 (ProjectSyncRunDrawer)
Then: T038 (ProjectDetailPage edit — depends on T036)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 (T001)
2. Complete Phase 2 foundational (T002–T014)
3. Complete Phase 3 US1 (T015–T028)
4. **STOP and VALIDATE**: click Sync on a real repo; confirm card live progress + metric persistence + retry on failure
5. Demo or deploy

### Incremental Delivery

| Milestone | Stories Delivered | Key Validation |
|-----------|-----------------|----------------|
| After Phase 3 | US1 | Single-repo sync + card states |
| After Phase 4 | US1 + US2 | Project-wide sync + strip + SSE |
| After Phase 5 | US1–US3 | Page loads metrics from DB only |
| After Phase 6 | US1–US4 | Contributors popover complete |
| After Phase 7 | All | E2E green + visual regression |

---

## Notes

- `[P]` = different files, no incomplete dependencies — safe to parallelize
- Domain tests (T002, T003) **must fail before** domain aggregates are implemented — Red-Green-Refactor per constitution
- No hardcoded hex colours anywhere in frontend — use CSS variables only (flagged in constitution check)
- Run `npm run generate-client` after adding `RepositorySyncsController` (before T024) and after adding project sync endpoints (before T034)
- The `ProjectSyncService.ExecuteAsync` method MUST stay under 30 lines — extract private helpers before the limit is reached (T029)
- Commit after each checkpoint; stop to validate the story independently before advancing to the next phase
