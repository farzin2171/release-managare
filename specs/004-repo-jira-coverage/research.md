# Research: Per-Repo Jira Coverage

**Phase 0 output** — All decisions resolved. No NEEDS CLARIFICATION items remain.

---

## Decision 1: Fix Version Naming Convention — `<RepoName>_<NextMinorVersion>`

**Decision**: The Jira fix version for each repository's next release is computed as `<RepositoryName>_<NextMinorVersion>`, where `NextMinorVersion` increments the minor semver segment of the current latest tag and resets patch to zero. The repository name is used exactly as it appears in the Git provider, preserving casing and punctuation (e.g., `Services.UX_1.31.0`).

**Rationale**: A deterministic naming scheme means the system can compute the fix version name independently, without any user configuration per repo. This contrasts with the existing release-reconciliation workflow (which uses a configurable fix-version-name pattern). The convention is consistent and predictable for teams: if a repository is at `1.30.0`, the next minor version is always `1.31.0` and the fix version is always `<RepoName>_1.31.0`. No ambiguity about which fix version to query.

**Alternatives considered**: Configurable per-repo fix version prefix — gives more flexibility but requires per-repo admin setup and creates drift between what the system checks and what teams actually use. Incrementing patch instead of minor — would not reflect the typical "next minor release" branching model used by the teams this feature targets.

---

## Decision 2: Semver Parsing — Domain Value Object with Leading-`v` Strip

**Decision**: A `SemVer` sealed record in `RepoManager.Domain/ValueObjects/` parses tag strings by stripping a leading `v` (if present) and then matching `MAJOR.MINOR.PATCH`. Pre-release labels (e.g., `-beta.1`) and build metadata (e.g., `+20260101`) are not supported in v1 and cause `TryParse` to return `false`. The `NextMinor()` method returns `new SemVer(Major, Minor + 1, 0)`.

**Rationale**: Keeping the version math in the domain layer keeps it pure, testable, and reusable. The strict parse (no pre-release, no metadata) matches the constitution's principle of simplicity and the spec's explicit non-goal of supporting non-standard schemes. Rejecting unrecognised formats at parse time gives a clean `Unsupported` signal to the comparison service rather than silently computing a wrong version.

**Test table** (from spec):

| Input tag  | `TryParse` | `ToString()` | `NextMinor()` |
|------------|-----------|--------------|---------------|
| `1.30.0`   | true       | `1.30.0`     | `1.31.0`      |
| `v2.5.7`   | true       | `2.5.7`      | `2.6.0`       |
| `0.9.0`    | true       | `0.9.0`      | `0.10.0`      |
| `release-2026` | false  | —            | —             |
| `1.0.0-beta.1` | false  | —            | —             |

---

## Decision 3: JSON Columns for Bucket Data — Consistent with Existing Pattern

**Decision**: The three ticket buckets (`InBothJson`, `JiraOnlyJson`, `GitOnlyJson`) and `UnmatchedCommitsJson` are stored as JSON strings in the `RepoJiraComparisonSnapshot` table rather than separate child tables.

**Rationale**: The existing `ReleaseReconciliationSnapshot` entity already uses this pattern. Staying consistent avoids introducing a new table-per-bucket schema that would require joins on every read. The data is read-mostly and replaced atomically on each sync — there is no partial update of individual tickets within a snapshot. JSON columns also simplify the EF Core configuration (single property, no child navigation) and keep the migration small.

**Alternatives considered**: Separate `ComparisonTicket` child tables — normalised but adds three tables, three EF configs, and join queries for every page load. Overkill for read-mostly snapshot data that is fully replaced on each sync. In-memory only (no persistence) — would not support background refresh or the 5-minute TTL guarantee; every page request would trigger a full Jira + Git API call.

---

## Decision 4: Cache Strategy — TTL + Background Refresh + Explicit Invalidation

**Decision**: Cached via a `LastSyncedAt` timestamp on `RepoJiraComparisonSnapshot`. On read: if `LastSyncedAt > now - 5 min`, return snapshot; else recompute. Background refresh: an `IHostedService` polls every 10 minutes and recomputes snapshots for repos where `LastViewedAt > now - 24 h`. Explicit invalidation: commit sync and tag changes set `LastSyncedAt = DateTime.MinValue` on the affected snapshot. Force re-sync: sets `forceRefresh: true`, bypassing the TTL check.

**Rationale**: The 5-minute TTL keeps the project page fast (no external API calls during page load when cache is warm) while preventing severely stale data from being displayed. The 24-hour view window scopes the background job to active repos only, avoiding unnecessary Jira API calls for repos nobody has looked at recently. Explicit invalidation ensures that commits-sync writes immediately mark the snapshot stale so the next load reflects the new commits.

**`LastViewedAt` column**: Added to the existing `Repository` entity (new EF column, nullable `DateTime?`). Updated via `SET LastViewedAt = now` whenever `GetForRepoAsync` is called.

**Alternatives considered**: Fixed poll for all tracked repos regardless of view recency — wastes Jira API quota on idle repos. Redis or distributed cache — out of scope for this platform; SQLite is the single source of truth per the constitution. Per-request recompute (no cache) — would make the project page unacceptably slow with many repos.

---

## Decision 5: IJiraService Extension — `GetTicketsInFixVersionAsync`

**Decision**: Add `GetTicketsInFixVersionAsync(IEnumerable<string> jiraProjectKeys, string fixVersionName, CancellationToken ct)` to the existing `IJiraService` interface. This method searches for all issues in the given Jira projects that have the named fix version. If the fix version does not exist, it returns an empty list (not an error).

**Rationale**: The existing `IJiraService` is an intentional extensibility seam (Constitution Principle VII). Adding a method to it is the correct place for new Jira operations. A dedicated method with clear semantics (`GetTicketsInFixVersionAsync`) is simpler than a generic JQL query method and can be mocked precisely in unit tests.

**Alternatives considered**: Generic JQL endpoint — more flexible but requires callers to construct JQL strings, introducing a query-language dependency in the application layer. `GetFixVersionByNameAsync` + `GetTicketsByFixVersionId` two-step — the Jira Cloud REST v3 `/rest/api/3/search` endpoint accepts a fix version name directly in JQL (`fixVersion = "<name>"`), so a single call suffices.

---

## Decision 6: `AddTicketToFixVersionAsync` — Create Fix Version on Demand

**Decision**: The `AddTicketToFixVersionAsync` method in `IRepoJiraComparisonService` calls Jira to add the ticket to the computed fix version. If the fix version does not exist, it calls `IJiraService.CreateFixVersionAsync(jiraProjectKey, fixVersionName, ct)` first, then adds the ticket. The entire operation is idempotent: if the ticket is already in the fix version, the Jira API returns a success-equivalent response and no duplicate is created.

**Rationale**: Creating the fix version on demand removes a user-facing friction point ("fix version not found" errors) and matches the stated spec behaviour. Jira's `PUT /rest/api/3/issue/{key}` with `fixVersions` update is idempotent — adding a version the issue already has does not fail. The spec non-goal explicitly states no automatic creation of tags in Git; the Jira-side creation is in scope.

**Alternatives considered**: Require the fix version to exist before allowing add — simpler but forces users to context-switch to Jira to create it; defeats the "close the gap without leaving the platform" goal.

---

## Decision 7: Background Refresh Service — `IHostedService`, Not Quartz

**Decision**: Use a plain `IHostedService` with a timer loop (10-minute interval) rather than Quartz.NET for the background refresh job.

**Rationale**: The platform has no Quartz.NET dependency today, and introducing a full job-scheduling framework for a single recurring task with no cron-expression requirements adds significant dependency weight. An `IHostedService` with `PeriodicTimer` (available in .NET 6+) is idiomatic, testable, and has no external dependencies. If more complex scheduling is needed in a future milestone, the class can be replaced without touching callers.

**Alternatives considered**: Quartz.NET — full scheduling framework with persistent job store, misfire handling, and cluster support. All of these are unnecessary for a single 10-minute poll. Hangfire — same objection; also requires a storage backend. `BackgroundService` + `Task.Delay` — functionally equivalent to `PeriodicTimer` but the newer API is preferred for .NET 10.
