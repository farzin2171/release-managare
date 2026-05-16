# `/plan` — Project Screen Sync Implementation Plan

> Paste after `/plan` once `/clarify` has resolved the four open questions in `01-specify.md`.

## Architectural placement

- **Domain**: two new aggregates `RepositorySync` and `ProjectSync` with their own state machines (`Pending → InProgress → Succeeded|Failed|Skipped`, and `Pending → InProgress → Succeeded|PartiallyFailed|Failed|Cancelled` respectively). State transitions are methods on the aggregate, not setters.
- **Application**: `IRepositorySyncService` and `IProjectSyncService` orchestrate the flow. Background work goes through a new `ISyncJobQueue` (typed wrapper over `Channel<SyncJob>`).
- **Infrastructure**: `SyncBackgroundService` (`IHostedService`) is the single consumer of the channel. SSE endpoint lives in the API layer using `Results.ServerSentEvents`.

No new NuGet dependencies. The existing `IConventionalCommitParser`, `IGitProviderService`, and EF Core context are reused.

## Backend — file-by-file

### Domain layer
1. `Domain/Aggregates/RepositorySync.cs` — entity with state-transition methods (`Start`, `Skip(reason)`, `SetStep(name)`, `Complete(counts, contributors)`, `Fail(message)`)
2. `Domain/Aggregates/ProjectSync.cs` — entity with `Start`, `RecordChildResult`, `Complete`, `Cancel`
3. `Domain/Enums/SyncStatus.cs`, `Domain/Enums/ProjectSyncStatus.cs`, `Domain/Enums/SyncStep.cs` (FetchingCommits, ParsingCommits, PersistingCommits, AggregatingTickets, Finalising)
4. `Domain/ValueObjects/ContributorSnapshot.cs` — record `(string Name, string Email, int Commits)`
5. Domain unit tests for every transition — `Repository.LatestTag` is null → `Skip("NoPinnedTag")` is the only legal transition from `Pending`; `SetStep` requires `InProgress`

### Persistence
6. EF configuration files for both new aggregates with the indexes from the spec
7. Migration `AddSyncTables` — creates both tables, adds the unique partial index on `ProjectSyncs` (`Status IN ('Pending', 'InProgress')`)
8. **No changes** to existing tables. `Commits` and `Tickets` are written by the existing services; the sync service calls them.

### Application services
9. `IRepositorySyncService` interface + implementation:
   - `Task<RepositorySync> EnqueueAsync(Guid repoId, Guid userId, Guid? parentProjectSyncId, CancellationToken)`
   - Creates the row in `Pending`, enqueues the job, returns immediately
   - Private `ExecuteAsync(Guid syncId)` runs the actual work; called only by the background worker
   - Emits `CurrentStep` transitions through an injected `ISyncEventPublisher` so the SSE channel sees them in real time
10. `IProjectSyncService` interface + implementation:
    - `Task<ProjectSync> EnqueueAsync(Guid projectId, Guid userId, CancellationToken)` — rejects with `ConflictException` if active run exists (unique index does the heavy lifting; catch `DbUpdateException` and translate)
    - `Task CancelActiveAsync(Guid projectId)` — flips status to `Cancelling`; worker observes the flag between repos
    - `Task ExecuteAsync(Guid projectSyncId)` — iterates assigned repos sequentially, calling `IRepositorySyncService.ExecuteAsync` for each
    - Publishes status events through `ISyncEventPublisher` — feeds the SSE endpoint
11. `IProjectSyncSnapshotService` — pure read service that joins the latest `RepositorySync` per repo for a project; backs the `/repositories/sync-snapshot` endpoint. Cached for 5s per project to absorb the screen-load burst.

### Background worker
12. `Infrastructure/Sync/SyncBackgroundService.cs` — `BackgroundService` reading from `ISyncJobQueue`. Resolves a scoped service provider per job. Catches all exceptions, marks the row failed, logs with the correlation ID.
13. On startup, run a "stale sync recovery" pass: any `InProgress` row older than 30 minutes is marked `Failed` with `ErrorMessage = "Stale — worker restarted"`. This is the answer to clarification question 2.

### Event publishing
14. `Infrastructure/Sync/InMemorySyncEventPublisher.cs` — keyed `Channel<SyncEvent>` per `projectSyncId` with a 30-minute TTL. The SSE endpoint subscribes; the application services publish. No persistence — events are ephemeral.

### API controllers
15. `RepositorySyncsController` — three endpoints from the spec
16. `ProjectSyncsController` — five endpoints from the spec, including the SSE stream and the snapshot endpoint
17. SSE implementation pattern:
    ```csharp
    [HttpGet("active/stream")]
    public async Task<IResult> Stream(Guid id, CancellationToken ct)
    {
        var events = _eventPublisher.SubscribeAsync(id, ct);
        return Results.ServerSentEvents(events);
    }
    ```
    Each `SyncEvent` is a record with `Type`, `RepoId`, `RepoName`, `Status`, `CurrentStep`, `ElapsedMs`, `Counts?`.

### Authorisation
- `[Authorize]` on all endpoints (any authenticated user)
- No role check — sync is replication, not privileged

## Frontend — file-by-file

The existing project detail page already renders the title row, stat cards, and repo card grid. We extend it without restructuring the page component.

### API client
1. `lib/api/syncApi.ts` — typed functions for all eight endpoints
2. `lib/api/syncSse.ts` — small wrapper around `EventSource` with auto-reconnect for the SSE stream

### Hooks
3. `features/projects/hooks/useProjectSyncSnapshot.ts` — `useQuery(['project', id, 'sync-snapshot'])` calls `/repositories/sync-snapshot` once on screen load; this is the single source for every card's metrics
4. `features/projects/hooks/useRepositorySync.ts` — `useMutation` for triggering; `useQuery` with 2s `refetchInterval` while a single-repo standalone sync is in flight; stops polling on terminal state
5. `features/projects/hooks/useProjectSync.ts` — `useMutation` for start/cancel; uses SSE for live progress; falls back to polling if SSE drops twice in a row; updates the cached snapshot in place as events arrive (no full refetch storm)

### New components
6. `features/projects/components/ProjectSyncStrip.tsx` — the new strip that sits between the title row and the stat cards. Three modes: idle (with last-synced timestamp), running (with X-of-N counter and Cancel button), just-completed (auto-dismisses after 30s). Uses existing card styling tokens — no custom CSS.
7. `features/projects/components/RepoCardSyncFooter.tsx` — the new bottom strip on every repo card. Shows last-synced timestamp + Sync/Retry/disabled button.
8. `features/projects/components/RepoCardSyncOverlay.tsx` — the centered status block that **replaces the existing four-metric grid** when the card is in `InProgress`, `Failed`, or `NoPinnedTag` state. Returns the metric grid view otherwise. Render decision lives here so the parent card stays simple.
9. `features/projects/components/ContributorsPopover.tsx` — popover anchored to the "contributors" metric label.
10. `features/projects/components/ProjectSyncRunDrawer.tsx` — the drawer opened by the strip's "View run" link, listing per-repo outcomes of the last `ProjectSync`.

### Existing component touch points (minimal edits)
11. `ProjectDetailPage.tsx` — insert `<ProjectSyncStrip />` between the existing title row and the existing `<UnreleasedChangesSummary />` (the stat cards). One line added; no other change.
12. `RepositoryCard.tsx` — wrap the existing four-metric grid in `<RepoCardSyncOverlay>` which decides whether to show the grid or a state overlay. Add `<RepoCardSyncFooter />` after the grid. Change the existing top-right `→ HEAD` text node to render the new tag chip. Three small edits to one file.
13. The existing stat cards' query (`useUnreleasedChanges`) is updated to read from the same snapshot as the cards — no separate aggregation pipeline.

### Empty/error states (already encoded in `RepoCardSyncOverlay`)
- No pinned tag → muted "Pin a latest tag to enable sync" + link to repo settings
- Never synced → metric grid shows zeros + footer reads "Not synced yet"
- Failed last run → metric grid replaced by error icon + reason; footer "Retry" button

## Testing strategy

### Domain unit tests
- State machine transitions, all happy paths and illegal transitions
- Idempotency: calling `Complete` twice on the same aggregate throws
- `SetStep` only callable while `InProgress`

### Infrastructure tests
- `SyncBackgroundService` consumes a queued job, executes it, persists the result (use Testcontainers SQLite)
- Stale-sync recovery on startup
- The unique partial index actually rejects a second concurrent `ProjectSync` row
- `InMemorySyncEventPublisher` delivers events to subscribers and drops channels after the TTL

### API integration tests
- Happy path: trigger repo sync → wait → assert row updates, `Commits`/`Tickets` populated, snapshot endpoint returns the new counts
- Project sync with one repo skipped (no tag), one succeeded, one forced-failure (mock provider throws) → final status `PartiallyFailed`, counts correct
- 409 returned on second project sync while first is running
- Cancellation: start a project sync with 3 repos, cancel after repo 1 completes, assert repos 2 and 3 are not run and final status is `Cancelled`
- SSE stream emits the expected event sequence end-to-end

### Frontend tests
- Unit: `useRepositorySync` polls while `Pending`/`InProgress`, stops on terminal
- Component: `RepoCardSyncOverlay` renders the right view for each of the five states; `ProjectSyncStrip` renders all three modes
- Playwright E2E: full project sync happy path with the SSE stream, asserting strip transitions and card overlays

### Visual regression
- Snapshot the project screen in three baselines: never-synced, partially-synced, all-synced. Catch any unintentional layout shift on the existing elements.

## Performance considerations

- Commit fetching is paginated by the Git provider — cap a single sync at 5,000 commits; if exceeded, fail with a clear message recommending a more recent tag pin
- Use EF `ExecuteUpdateAsync` / batch inserts for the commits batch; never iterate with `SaveChanges` per row
- The snapshot endpoint is a single SQL query with a `LATERAL` join to the latest `RepositorySync` per repo — cached 5s per project at the application layer
- The SSE endpoint streams from an in-memory `Channel<SyncEvent>` keyed by `projectSyncId` with a 30-minute TTL — events are not persisted

## Observability

- Structured log per repo sync: correlation ID, repo name, commit count, tickets count, elapsed ms, outcome
- Audit log entry per project sync start/complete/cancel with the triggering user
- Counters: `sync.repository.completed`, `sync.repository.failed`, `sync.project.completed` exposed via the existing `/metrics` endpoint

## Migration & rollout

- Single EF migration, no data backfill
- No feature flag — the new strip simply shows "Never synced" and the new card footers show "Not synced yet" until the user triggers their first sync. Existing zero-valued stat cards remain valid.
- The existing per-repo change-detail screen continues to work via its existing endpoint; that endpoint is now also a consumer of the same persisted `Commits`/`Tickets` rows, but its API contract is unchanged

## Estimated effort

| Bucket | Days |
|--------|------|
| Domain + EF + migration | 0.5 |
| Repository sync service + background worker | 1 |
| Project sync orchestrator + SSE + event publisher | 1 |
| Snapshot endpoint + API tests | 0.5 |
| Frontend strip + card overlay + footer | 1 |
| Frontend SSE wiring + popover + run drawer | 1 |
| QA + visual regression + polish | 0.5 |
| **Total** | **5.5** |
