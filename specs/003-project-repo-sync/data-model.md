# Data Model: Project Screen — Repository Sync & Changes Persistence

**Branch**: `003-project-repo-sync` | **Date**: 2026-05-16

All changes are additive. No existing table is modified.

---

## New Table: `RepositorySyncs`

One row per sync run for a `(Repository, FromTag)` pair. The **active snapshot** for a card is the row where `Status = Succeeded`, `FromTag = Repository.LatestTag`, ordered by `StartedAt DESC`, taking the first result.

| Column | EF Core Type | Nullable | Constraints | Notes |
|--------|-------------|----------|-------------|-------|
| `Id` | `Guid` | No | PK | `Guid.NewGuid()` |
| `RepositoryId` | `Guid` | No | FK → `Repositories.Id` | Cascade delete |
| `ProjectSyncId` | `Guid?` | Yes | FK → `ProjectSyncs.Id` | NULL for standalone repo syncs; Set Null on delete |
| `FromTag` | `string(100)` | No | — | Snapshot of `Repository.LatestTag` at sync time |
| `ToCommitSha` | `string(64)` | Yes | — | HEAD SHA at sync time; NULL until `InProgress` |
| `Status` | `SyncStatus` (int enum) | No | — | See state machine below |
| `SkipReason` | `string(200)?` | Yes | — | Populated when `Skipped` (e.g., `"NoPinnedTag"`) |
| `CurrentStep` | `string(50)?` | Yes | — | Populated while `InProgress` (see `SyncStep` enum) |
| `StartedAt` | `DateTimeOffset` | No | — | UTC; set when row is created |
| `CompletedAt` | `DateTimeOffset?` | Yes | — | UTC; NULL while running |
| `CommitCount` | `int` | No | Default 0 | Set on completion |
| `TicketCount` | `int` | No | Default 0 | Unique Jira tickets; set on completion |
| `ContributorCount` | `int` | No | Default 0 | Unique by email (name fallback); set on completion |
| `BreakingChangeCount` | `int` | No | Default 0 | Set on completion |
| `ContributorsJson` | `string` | No | Default `"[]"` | JSON array of `ContributorSnapshot` |
| `ErrorMessage` | `string(1000)?` | Yes | — | Populated on `Failed` |
| `TriggeredByUserId` | `Guid` | No | FK → `Users.Id` | Restrict delete |

**Indexes**:
- `(RepositoryId, FromTag, Status, StartedAt DESC)` — composite; covers active-snapshot lookup
- `ProjectSyncId` — for project-run rollup query

---

## New Table: `ProjectSyncs`

One row per project-wide sync run.

| Column | EF Core Type | Nullable | Constraints | Notes |
|--------|-------------|----------|-------------|-------|
| `Id` | `Guid` | No | PK | `Guid.NewGuid()` |
| `ProjectId` | `Guid` | No | FK → `Projects.Id` | Cascade delete |
| `Status` | `ProjectSyncStatus` (int enum) | No | — | See state machine below |
| `StartedAt` | `DateTimeOffset` | No | — | UTC |
| `CompletedAt` | `DateTimeOffset?` | Yes | — | UTC |
| `TotalRepos` | `int` | No | — | Count of assigned repos at enqueue time |
| `SucceededCount` | `int` | No | Default 0 | |
| `FailedCount` | `int` | No | Default 0 | |
| `SkippedCount` | `int` | No | Default 0 | |
| `TriggeredByUserId` | `Guid` | No | FK → `Users.Id` | Restrict delete |

**Indexes**:
- `(ProjectId, Status, StartedAt DESC)` — covers latest-run lookup per project
- **Unique partial index** on `ProjectId` where `Status IN (Pending=0, InProgress=1)` — enforces single active run per project at the DB level

---

## Enums

### `SyncStatus` (used by `RepositorySyncs`)

| Value | Int | Meaning |
|-------|-----|---------|
| `Pending` | 0 | Row created; job not yet picked up by worker |
| `InProgress` | 1 | Worker is actively running this sync |
| `Succeeded` | 2 | Sync completed successfully |
| `Failed` | 3 | Sync ended with an error |
| `Skipped` | 4 | Skipped (e.g., no pinned tag) |

### `ProjectSyncStatus` (used by `ProjectSyncs`)

| Value | Int | Meaning |
|-------|-----|---------|
| `Pending` | 0 | Run enqueued; worker not started |
| `InProgress` | 1 | Worker is processing repos sequentially |
| `Succeeded` | 2 | All repos completed (0 failures) |
| `PartiallyFailed` | 3 | At least one repo failed, at least one succeeded |
| `Failed` | 4 | All repos failed or errored at the run level |
| `Cancelled` | 5 | User cancelled; in-flight repo completed before stop |

### `SyncStep` (string constants on `RepositorySyncs.CurrentStep`)

| Constant | Display label |
|----------|--------------|
| `FetchingCommits` | "Fetching commits…" |
| `ParsingCommits` | "Parsing N commits…" |
| `PersistingCommits` | "Persisting tickets…" |
| `AggregatingTickets` | "Aggregating tickets…" |
| `Finalising` | "Finalising…" |

Stored as a `string` column (not an int enum) so the label can carry dynamic content (e.g., commit count interpolated before persisting).

---

## State Machines

### `RepositorySync` transitions

```
[created] → Pending
Pending   → InProgress      (worker picks up job)
Pending   → Skipped         (no pinned tag; only legal transition from Pending when tag is absent)
InProgress → Succeeded      (all steps complete)
InProgress → Failed         (unrecoverable error after retries)
```

Illegal transitions (must throw in domain aggregate): any other combination.
Calling `Complete()` or `Fail()` on a non-`InProgress` record throws `InvalidOperationException`.

### `ProjectSync` transitions

```
[created] → Pending
Pending   → InProgress      (worker starts processing repos)
InProgress → Succeeded      (all repos done, 0 failures)
InProgress → PartiallyFailed (at least one failure, at least one success)
InProgress → Failed         (all repos failed / unexpected run-level error)
InProgress → Cancelled      (user cancelled; in-flight repo completed first)
```

---

## Value Object: `ContributorSnapshot`

Stored as JSON in `RepositorySyncs.ContributorsJson`. Not a separate table.

```json
[
  { "name": "Alice Martin", "email": "alice@example.com", "commits": 12 },
  { "name": "Bob Chen",     "email": "bob@example.com",   "commits": 4  }
]
```

Deduplication key within a single sync: `email.ToLower()`, falling back to `name.ToLower()` when email is null or empty.

---

## Reused Tables (no changes)

| Table | How used |
|-------|----------|
| `Commits` | Sync writes/upserts rows keyed by `(RepositoryId, Sha)` via existing service |
| `Tickets` | Sync writes/upserts per-ticket aggregates via existing service |
| `Repositories` | Read `LatestTag` and `DefaultBranch` at sync time |
| `Projects` | Read assigned repos list for project-wide sync |
| `Users` | Read for `TriggeredByUserId` FK |

---

## DTOs (Application layer)

### `RepositorySyncDto`
```
Id, RepositoryId, ProjectSyncId?, FromTag, ToCommitSha?, Status, SkipReason?,
CurrentStep?, StartedAt, CompletedAt?, CommitCount, TicketCount, ContributorCount,
BreakingChangeCount, Contributors (List<ContributorSnapshotDto>), ErrorMessage?
```

### `ProjectSyncDto`
```
Id, ProjectId, Status, StartedAt, CompletedAt?, TotalRepos,
SucceededCount, FailedCount, SkippedCount, TriggeredByUserId,
ChildSyncs (List<RepositorySyncDto>)  ← included on detail endpoint only
```

### `RepoSyncSnapshotItemDto`
```
RepositoryId, RepositoryName, LatestTag?, LatestSync (RepositorySyncDto?), CurrentStep?
```
One item per repository in the project; `LatestSync` is null when the repo has never been synced against its current tag.
