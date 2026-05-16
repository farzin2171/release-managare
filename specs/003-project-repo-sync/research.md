# Research: Project Screen — Repository Sync & Changes Persistence

**Branch**: `003-project-repo-sync` | **Date**: 2026-05-16

## Decision Log

---

### R-001: Server-Sent Events in .NET 10

**Decision**: Use `Results.ServerSentEvents(IAsyncEnumerable<SseItem<T>>)` from `Microsoft.AspNetCore.Http.HttpResults`, available natively in .NET 8+.

**Rationale**: Zero new dependencies. The built-in API handles chunked encoding, `Content-Type: text/event-stream`, and connection teardown on client disconnect. The controller returns `IResult` and lets the runtime stream the `IAsyncEnumerable`.

**Pattern**:
```csharp
[HttpGet("active/stream")]
public IResult Stream(Guid id, CancellationToken ct)
{
    var events = _eventPublisher.SubscribeAsync(id, ct);
    return Results.ServerSentEvents(events.Select(e =>
        new SseItem<SyncEvent>(e, eventType: e.Type)));
}
```

**Alternatives considered**:
- SignalR — rejected: adds runtime dependency; overkill for one-directional event stream.
- Long-polling — rejected: higher server resource cost; worse UX (gaps between polls).

---

### R-002: Background Worker Pattern (Channel-based Queue)

**Decision**: `Channel<SyncJob>` as the backing store, wrapped in `ISyncJobQueue` (single implementation: `InMemorySyncJobQueue`). `SyncBackgroundService : BackgroundService` reads from the channel, resolves a scoped `IServiceProvider` per job, and executes the sync.

**Rationale**: This is the idiomatic .NET hosted-service pattern with no new dependencies. Scoped DI resolution per job ensures EF contexts are not shared across concurrent jobs. A single background service instance is sufficient for the current scale (sequential project syncs; standalone repo syncs can be queued concurrently).

**Stale recovery**: On `StartAsync`, the worker queries for any `RepositorySync` or `ProjectSync` rows with `Status = InProgress` and `StartedAt < UtcNow - 30 minutes`, marks them `Failed` with `ErrorMessage = "Stale — worker restarted"`, and logs at Warning level.

**Alternatives considered**:
- Hangfire — rejected: adds NuGet dependency; persistence is unnecessary since jobs are recreatable by re-clicking Sync.
- Quartz.NET — rejected: same reason; this queue is fire-and-forget, not scheduled.

---

### R-003: In-Memory Keyed Event Publisher

**Decision**: `InMemorySyncEventPublisher` maintains a `ConcurrentDictionary<Guid, Channel<SyncEvent>>` keyed by `projectSyncId`. Channels have a bounded capacity of 100 events. Publisher writes to the channel; SSE endpoint consumes via `ReadAllAsync`. Channels are removed from the dictionary 30 minutes after the project sync reaches a terminal status (cleanup via `Task.Delay` in a fire-and-forget continuation).

**Rationale**: All SSE consumers exist in the same process (single-container deployment). The in-memory channel avoids Redis or any broker dependency. The 30-minute TTL prevents memory leaks for abandoned browser tabs. Bounded capacity (100) provides backpressure — if the consumer disconnects and the producer keeps running, writes will block briefly then succeed once the consumer reconnects, or drop if the channel overflows (acceptable for progress events).

**Alternatives considered**:
- Redis Pub/Sub — rejected: no Redis in the approved stack; single-container deployment makes it unnecessary.
- Persistent event log — rejected: progress events are ephemeral; persisting them provides no business value.

---

### R-004: SQLite Unique Partial Index (EF Core)

**Decision**: Use `HasIndex` with `HasFilter` in `ProjectSyncConfiguration` to create the unique partial index that prevents two active `ProjectSync` rows for the same project.

**Pattern**:
```csharp
builder.HasIndex(ps => ps.ProjectId)
       .HasFilter("Status IN (0, 1)")  // 0=Pending, 1=InProgress
       .IsUnique();
```

**Conflict handling**: `ProjectSyncService.EnqueueAsync` wraps `SaveChangesAsync` in a try/catch for `DbUpdateException`. If the unique index raises a constraint violation, the service translates it to `ConflictException` (HTTP 409), which the global exception handler maps to a ProblemDetails response.

**Rationale**: Enforcement at the DB level is safer than application-level locking — it survives race conditions between concurrent requests.

**Alternatives considered**:
- Application-level lock (`SemaphoreSlim`) — rejected: doesn't survive multiple server instances and is harder to test.
- Checking existence before insert — rejected: classic TOCTOU race condition.

---

### R-005: Transient Error Classification for Git Provider Retries

**Decision**: The existing Polly `HttpClient` retry policy (3 retries, exponential backoff, triggered on HTTP 429 and 5xx) already handles transient errors at the HTTP client level for `IGitProvider`. The sync service does not need to implement its own retry loop.

**Clarification mapping**: FR-023 (auto-retry up to 3 times for transient errors) is satisfied by the existing Polly policy. Permanent errors (401, 403, 404) fall outside the Polly trigger conditions and propagate immediately as exceptions, which the sync service catches and maps to `Failed` status with the error message.

**Rationale**: Reusing the existing Polly policy avoids duplicating retry logic. The constitution (Section "External API resilience") mandates this policy for all HttpClient-based integrations.

**No new code required**: This is a confirmation that existing infrastructure covers FR-023.

---

### R-006: TanStack Query SSE + Polling Hybrid (Frontend)

**Decision**: `useProjectSync` hook subscribes to the SSE stream for live project-sync progress. If the `EventSource` fires `onerror` twice consecutively, the hook falls back to polling the `/projects/{id}/sync/active` endpoint at a 3-second interval. On reconnect or poll-detected terminal state, SSE is re-attempted.

`useRepositorySync` uses `useQuery` with `refetchInterval: 2000` while the sync status is `Pending` or `InProgress`; sets `refetchInterval: false` on terminal state. This is sufficient for single-repo standalone sync progress (no SSE needed per the spec assumption).

**Rationale**: SSE is preferable for project-level sync (many events, user watches a live progress board). Polling at 2s is specified in the spec assumptions for single-repo sync and avoids the complexity of opening a second SSE channel per card.

**Alternatives considered**:
- WebSocket — rejected: overkill for server-to-client-only events.
- Full polling for project sync — rejected: higher server load; worse UX with visible lag between card state changes.

---

### R-007: Snapshot Endpoint Query Design

**Decision**: `GET /api/v1/projects/{id}/repositories/sync-snapshot` is backed by a single SQL query using a lateral/correlated subquery to fetch the latest successful `RepositorySync` per repo for each repo's current `LatestTag`.

**EF Core pattern** (Infrastructure service):
```csharp
var snapshot = await _ctx.Repositories
    .Where(r => r.ProjectId == projectId)
    .Select(r => new RepoSyncSnapshotItemDto(
        r.Id,
        r.LatestTag,
        _ctx.RepositorySyncs
            .Where(s => s.RepositoryId == r.Id
                     && s.FromTag == r.LatestTag
                     && s.Status == SyncStatus.Succeeded)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault()
    ))
    .ToListAsync(ct);
```

Application-layer cache: 5-second sliding expiration per `projectId` using `IMemoryCache` to absorb the screen-load burst when multiple users open the same project simultaneously.

**Rationale**: Single round-trip to the DB for all card data. The cache TTL of 5s is low enough that metric updates appear within seconds of a sync completing.

---

### R-008: Contributor Deduplication

**Decision**: Within a single repo sync, contributors are deduplicated by lowercased email. When email is null or empty (e.g., noreply addresses with no `@`-prefixed local part), the lowercased display name is used as the fallback key. The deduplication runs in-process after commits are fetched, before the `ContributorsJson` snapshot is written.

For the project-level "CONTRIBUTORS" stat card, the frontend aggregates contributor snapshots from all synced repos, builds a `Set` keyed by `email.toLowerCase() || name.toLowerCase()`, and counts the set size. This matches FR-018 without a separate backend aggregate call.

**Rationale**: Keeping deduplication in-memory (both backend per-sync and frontend for project total) avoids a complex DB query and is fast at the scales involved (typically < 50 contributors per project).
