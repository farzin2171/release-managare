# Plan Addendum — Per-Repo Jira Visibility

Addition to `03-plan.md`. Covers data model, services, API endpoints, and frontend components.

---

## Data Model — New Entity

### `RepoJiraComparisonSnapshot`

Cached per-repo comparison result. One row per `(RepositoryId, NextVersion)` pair.

```csharp
public class RepoJiraComparisonSnapshot
{
    public int Id { get; set; }
    public int RepositoryId { get; set; }
    public Repository Repository { get; set; } = null!;

    // The version we computed and compared against
    public string CurrentTag { get; set; } = "";           // e.g., "1.30.0"
    public string NextVersion { get; set; } = "";          // e.g., "1.31.0"
    public string JiraFixVersionName { get; set; } = "";   // e.g., "Services.UX_1.31.0"
    public bool JiraFixVersionExists { get; set; }

    // Counts
    public int CommitCount { get; set; }
    public int GitTicketCount { get; set; }
    public int JiraTicketCount { get; set; }
    public int InBothCount { get; set; }
    public int JiraOnlyCount { get; set; }
    public int GitOnlyCount { get; set; }
    public decimal MatchRate { get; set; }                 // 0.0–1.0

    // Bucket detail as JSON
    public string InBothJson { get; set; } = "[]";
    public string JiraOnlyJson { get; set; } = "[]";
    public string GitOnlyJson { get; set; } = "[]";
    public string UnmatchedCommitsJson { get; set; } = "[]";

    public DateTime LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }
}
```

Index: unique on `(RepositoryId, NextVersion)`.

The bucket data is stored as JSON rather than separate child tables because (a) it's read-mostly with full-snapshot replacement on each sync, and (b) the existing `ReleaseReconciliationSnapshot` already uses this pattern in the codebase — stay consistent.

---

## Domain — Version Helper

A small value object for semver math, in `RepoManager.Domain/ValueObjects/`.

```csharp
public sealed record SemVer(int Major, int Minor, int Patch)
{
    public static bool TryParse(string tag, out SemVer? result)
    {
        // Strip leading 'v' if present, parse MAJOR.MINOR.PATCH
        // Reject anything with pre-release or build metadata for v1
    }

    public SemVer NextMinor() => new(Major, Minor + 1, 0);

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
```

Used by the comparison service to compute the next version. Unit-tested with the table from the spec.

---

## Application Layer

### `IRepoJiraComparisonService` (interface)

```csharp
public interface IRepoJiraComparisonService
{
    Task<RepoJiraComparisonDto> GetForRepoAsync(
        int repositoryId,
        bool forceRefresh,
        CancellationToken ct);

    Task<IReadOnlyList<RepoJiraComparisonDto>> GetForProjectAsync(
        int projectId,
        bool forceRefresh,
        CancellationToken ct);

    Task<AddToFixVersionResultDto> AddTicketToFixVersionAsync(
        int repositoryId,
        string ticketKey,
        CancellationToken ct);
}
```

Implementation in `RepoManager.Infrastructure/Jira/RepoJiraComparisonService.cs`. Composition:

- `IGitProvider` — for commits since last tag (already exists)
- `IJiraService` — for fix-version tickets (already exists; may need a `GetFixVersionByNameAsync` helper)
- `AppDbContext` — for snapshot persistence

### Algorithm (pseudocode)

```csharp
async Task<RepoJiraComparisonDto> ComputeAsync(Repository repo, CancellationToken ct)
{
    // 1. Get latest tag
    var currentTag = await _git.GetLatestSemVerTagAsync(repo, ct);
    if (!SemVer.TryParse(currentTag, out var current))
        return RepoJiraComparisonDto.Unsupported(repo, "non-semver tag");

    var next = current.NextMinor();
    var fixVersionName = $"{repo.Name}_{next}";

    // 2. Get commits since last tag, extract ticket IDs
    var commits = await _git.GetCommitsSinceTagAsync(repo, currentTag, ct);
    var gitTicketKeys = commits
        .Select(c => _parser.ExtractJiraTicket(c.Message))
        .Where(k => k is not null)
        .Cast<string>()
        .ToHashSet();

    // 3. Fetch Jira fix version by exact name
    var jiraProjectKeys = repo.Projects.SelectMany(p => p.JiraProjectKeys).Distinct();
    var jiraTickets = await _jira.GetTicketsInFixVersionAsync(
        jiraProjectKeys, fixVersionName, ct);
    var jiraKeys = jiraTickets.Select(t => t.Key).ToHashSet();

    // 4. Compute buckets
    var inBoth = jiraKeys.Intersect(gitTicketKeys).ToList();
    var jiraOnly = jiraKeys.Except(gitTicketKeys).ToList();
    var gitOnly = gitTicketKeys.Except(jiraKeys).ToList();

    var union = jiraKeys.Union(gitTicketKeys).Count();
    var matchRate = union == 0 ? 1.0m : (decimal)inBoth.Count / union;

    // 5. Persist snapshot, return DTO
    // ...
}
```

The unique parts versus the existing reconciliation service:

- **No fix-version pattern lookup** — the name is computed deterministically from repo name + next version
- **Repo-scoped, not project-scoped** — operates on one repo at a time
- **Cache-friendly** — designed for the project page to call once per repo and complete fast

### Cache strategy

- Read path: `GetForRepoAsync(forceRefresh: false)` returns the snapshot if `LastSyncedAt > now - 5 min`, otherwise recomputes
- Background refresh job runs every 10 minutes (Quartz.NET or `IHostedService`) for repos viewed in last 24 hours (track via a `LastViewedAt` column on `Repository`)
- Invalidation: when commits sync writes new rows, set `LastSyncedAt = DateTime.MinValue` for the affected repo's snapshot

---

## API Endpoints

Add to `RepoManager.Api/Controllers/`:

```
GET    /api/v1/repositories/{id}/jira-coverage
       ?refresh=true|false
       → RepoJiraComparisonDto

GET    /api/v1/projects/{id}/jira-coverage
       ?refresh=true|false
       → ProjectJiraCoverageDto { aggregate, repos: [RepoJiraComparisonDto] }

POST   /api/v1/repositories/{id}/jira-coverage/add-ticket
       Body: { ticketKey: "PROJ-1234" }
       → AddToFixVersionResultDto
       Admin only
```

DTO shape for `RepoJiraComparisonDto`:

```csharp
public record RepoJiraComparisonDto(
    int RepositoryId,
    string RepositoryName,
    string? CurrentTag,
    string? NextVersion,
    string? JiraFixVersionName,
    bool JiraFixVersionExists,
    bool Supported,                   // false if non-semver tag etc.
    string? UnsupportedReason,
    Counts Counts,
    decimal MatchRate,
    HealthBand Health,                // Green / Amber / Red / Unknown
    IReadOnlyList<TicketSummary> InBoth,
    IReadOnlyList<TicketSummary> JiraOnly,
    IReadOnlyList<TicketSummary> GitOnly,
    IReadOnlyList<CommitSummary> UnmatchedCommits,
    DateTime LastSyncedAt
);
```

---

## Frontend Components (React + shadcn)

New files under `frontend/src/features/jira-coverage/`:

- `RepoCoverageCard.tsx` — used on the project page, one per repo
- `ProjectCoverageAggregate.tsx` — header strip with project-wide totals
- `RepoCoverageTab.tsx` — the full "Jira coverage" tab on the service page
- `BucketList.tsx` — reusable three-bucket renderer (in both / Jira only / Git only), shared with the existing release reconciliation view if practical
- `HealthPill.tsx` — coloured pill component for match rate
- `useJiraCoverage.ts` — React Query hook wrapping the API endpoints

Routing additions:

- `/projects/:projectId` — existing page, render cards in a grid below the existing project content
- `/repositories/:repoId` — existing page, add the `Jira coverage` tab to the existing tab strip

shadcn primitives to lean on: `Card`, `Tabs`, `Badge`, `Tooltip`, `Collapsible`, `Skeleton` (for loading states on the cards while caches warm), `Button` for re-sync.

---

## Telemetry

Log structured events:

- `jira_coverage.computed` — repoId, durationMs, gitTicketCount, jiraTicketCount, matchRate
- `jira_coverage.cache_hit` / `cache_miss`
- `jira_coverage.add_to_fix_version` — repoId, ticketKey, userId, success
- `jira_coverage.compute_failed` — repoId, exceptionType, message

These let you tune the cache TTL and spot Jira API rate-limit hits.
