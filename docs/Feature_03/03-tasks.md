# `/tasks` Guidance — Project Screen Sync

> Cross-check Spec Kit's generated `tasks.md` against this list. Twelve ordered tasks, each individually shippable behind a status check.

## T1 — Domain aggregates + state machines
- `RepositorySync` and `ProjectSync` aggregates with state-transition methods, including `SetStep`
- Enums (`SyncStatus`, `ProjectSyncStatus`, `SyncStep`) and value objects (`ContributorSnapshot`)
- Full unit-test coverage on all transitions, including illegal ones
- **Check**: all domain tests green; no other code depends on these yet

## T2 — Persistence + migration
- EF configurations for both aggregates with the indexes from the spec
- The unique partial index on `ProjectSyncs.Status` for `Pending`/`InProgress` per `ProjectId`
- Migration `AddSyncTables`, applied on dev DB
- **Check**: migration applies cleanly, indexes verified via `sqlite_master`

## T3 — Sync job queue, event publisher, background worker scaffold
- `ISyncJobQueue` wrapping a bounded `Channel<SyncJob>`
- `InMemorySyncEventPublisher` with TTL'd per-project channels
- `SyncBackgroundService` reading from the queue and resolving a scoped service provider per job
- Stale-sync recovery on startup
- **Check**: integration test enqueues a noop job, asserts it's picked up, a log line is emitted, and a published event reaches a subscriber

## T4 — Repository sync service
- `IRepositorySyncService` interface + implementation
- Enqueue path returns a `Pending` row immediately
- Execute path: walk the five steps, emit `CurrentStep` events at each phase boundary, persist the final row
- Reuses `IConventionalCommitParser`, `IGitProviderService.ListCommitsAsync`, and the existing ticket aggregation pass
- 5,000-commit cap with a clear failure message
- **Check**: integration test against a fake Git provider syncs 50 commits, asserts counts, idempotency on re-run, all five `CurrentStep` events published in order

## T5 — Repository sync API
- `POST /repositories/{id}/sync`, `GET /repositories/{id}/sync/latest`, `GET /repository-syncs/{id}`
- `[Authorize]` on all (no role gate)
- Returns 202 on enqueue, includes the created sync row in the body
- **Check**: contract tests for each endpoint; the no-pinned-tag case creates a `Skipped` row, not a 422

## T6 — Project sync orchestrator
- `IProjectSyncService` with enqueue / cancel / execute / subscribe methods
- Sequential per-repo execution, failure does not stop the run
- Cancellation observed between repos
- Final status computation (`Succeeded` / `PartiallyFailed` / `Failed` / `Cancelled`)
- 409 on concurrent enqueue (catch `DbUpdateException`, translate)
- **Check**: integration test with 3 fake repos (1 succeeds, 1 fails, 1 has no tag) → status is `PartiallyFailed` with correct counts

## T7 — Project sync SSE endpoint + remaining APIs
- `GET /projects/{id}/sync/active/stream` returns `text/event-stream`
- Final `complete` event with the run summary
- `POST /projects/{id}/sync`, `DELETE /projects/{id}/sync/active`, `GET /projects/{id}/sync/latest`, `GET /projects/{id}/sync/active`
- **Check**: manual `curl -N` test against a running project sync emits the expected event sequence; full API contract test pass

## T8 — Snapshot endpoint
- `GET /projects/{id}/repositories/sync-snapshot` returns one row per assigned repo with the latest `RepositorySync` joined in
- 5s application-layer cache per project
- The existing "unreleased changes" stat-card query is rewritten to read from this same data source
- **Check**: snapshot returns correct data after one repo synced, after all synced, after one failed; cache key invalidates on any sync completion for the project

## T9 — Frontend API client + hooks
- `syncApi.ts` typed functions for all eight endpoints
- `syncSse.ts` with auto-reconnect
- `useProjectSyncSnapshot` — single screen-load query
- `useRepositorySync` — polling at 2s while in flight, stops on terminal
- `useProjectSync` — SSE-driven with polling fallback; patches the cached snapshot as events arrive
- **Check**: hook unit tests via MSW; polling stops on terminal state; snapshot patches correctly on each event type

## T10 — Frontend strip + card components
- `ProjectSyncStrip` with idle/running/just-completed modes
- `RepoCardSyncFooter` with the small timestamp + button
- `RepoCardSyncOverlay` that decides whether to render the existing four-metric grid or one of the three state overlays (in-progress, failed, no-pinned-tag)
- `ContributorsPopover` lists names and per-author commit counts
- `ProjectSyncRunDrawer` for the "View run" link
- **Check**: each component renders correctly in isolation across all states (use Storybook stories if available)

## T11 — Page integration
- `ProjectDetailPage.tsx` — single line added to insert `<ProjectSyncStrip />`
- `RepositoryCard.tsx` — three small edits: tag chip in top right, wrap metric grid in `<RepoCardSyncOverlay>`, append `<RepoCardSyncFooter />`
- Existing "unreleased changes" stat cards continue to work, now backed by real data after the first sync
- **Check**: visual diff of the page in three baselines (never-synced, partially-synced, all-synced) — only the three intended additions appear; existing layout pixel-stable

## T12 — Polish + docs + observability
- Audit log entries for every project sync start/complete/cancel
- Structured-log fields documented in the README
- Metrics counters wired
- README section on the sync model, including "what triggers a sync", "what data it persists", "how to recover from a stuck sync"
- Playwright E2E: full Tech-lead end-to-end pass — pin tag → sync repo → reload page → data reads from DB without provider call → sync whole project → strip and cards update live
- **Check**: full Tech-lead end-to-end pass on a real project with multiple repos
