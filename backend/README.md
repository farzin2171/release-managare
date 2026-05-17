# RepoManager Backend

ASP.NET Core Web API for the Repository Release Management Platform.

## Repository Sync

### What triggers a sync

Syncs are **manual only** in v1. A sync is initiated in two ways:

- **Single-repository sync**: `POST /api/v1/repositories/{id}/sync` — triggered by a user clicking the "Sync" button on a repository card. Requires the repository to have a pinned latest tag; if no tag is set the sync is immediately recorded as `Skipped`.
- **Project-wide sync**: `POST /api/v1/projects/{id}/sync` — triggered by clicking "Sync project". Iterates all repositories in the project sequentially; repositories without a pinned tag are auto-skipped; one failure does not stop the run.

### What data is persisted

Each sync run writes to three tables:

| Table | Content |
|-------|---------|
| `RepositorySyncs` | One row per sync run: status, step, counts (commits, tickets, contributors, breaking changes), error message, contributor JSON snapshot |
| `Commits` | Upserted by `(RepositoryId, Sha)` — no duplicates across re-syncs |
| `Tickets` | Replaced per `(RepositoryId, FromTag)` — aggregated from commit Jira ticket references |

Contributor data is stored as a JSON column on `RepositorySyncs` — a de-duplicated list of `{ name, email, commits }` objects, keyed by lowercased email (name used as fallback when email is absent).

The commit cap is **5,000 commits per sync run**. Exceeding this limit causes the sync to fail with a clear error message.

### How metrics are read on page load

The project detail screen calls `GET /api/v1/projects/{id}/repositories/sync-snapshot` on load. This endpoint returns the latest successful `RepositorySync` per repository for each repo's current pinned tag, sourced entirely from SQLite. **No Git provider call is made at page-load time.**

The snapshot is cached in-process with a 5-second sliding window per project. The cache is invalidated whenever any child sync completes for a repo in that project.

If a repository's pinned tag changes after a sync, the snapshot treats that repo's `LatestSync` as `null` (i.e., "not yet synced against the current tag"), so the card reverts to "Not synced yet".

### How to recover a stuck sync

**Automatic recovery**: On worker restart, `SyncBackgroundService.StartAsync` marks any `RepositorySync` or `ProjectSync` rows in the `InProgress` state older than 30 minutes as `Failed` / `Cancelled` respectively, with `ErrorMessage = "Stale — worker restarted"`.

**Manual recovery (development only)**: Update the row directly in SQLite:

```sql
UPDATE RepositorySyncs SET Status = 3, ErrorMessage = 'Manually recovered', CompletedAt = unixepoch('now') * 10000000 + 621355968000000000 WHERE Id = '<sync-id>';
```

Status values: `0 = Pending`, `1 = InProgress`, `2 = Succeeded`, `3 = Failed`, `4 = Skipped`.

### Observability

Each completed sync emits a structured Serilog log entry at `Information` or `Warning` level with the properties:

| Property | Description |
|----------|-------------|
| `CorrelationId` | The sync row GUID (acts as the per-job correlation ID in background context) |
| `RepoName` | Repository display name |
| `CommitCount` | Commits fetched in this run |
| `TicketCount` | Unique Jira tickets found |
| `ElapsedMs` | Wall-clock duration |
| `Outcome` | `Succeeded` or `Failed` |
| `ErrorMessage` | Failure reason (Warning level only) |

Project sync runs additionally emit `Information` logs on start (user ID, project ID, repo count), completion (status, counts, elapsed), and cancellation (user ID, repos processed vs. total).

Metrics counters (`System.Diagnostics.Metrics`) are emitted under the `RepoManager.Sync` meter:

| Counter | Incremented when |
|---------|-----------------|
| `sync.repository.completed` | A repository sync finishes with `Succeeded` |
| `sync.repository.failed` | A repository sync finishes with `Failed` |
| `sync.project.completed` | A project sync run reaches any terminal state |
