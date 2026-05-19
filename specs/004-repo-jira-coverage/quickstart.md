# Quickstart: Per-Repo Jira Coverage Feature

Developer notes for implementing and testing the `004-repo-jira-coverage` feature on top of the existing platform.

---

## Prerequisites

This feature builds on milestones 1–9 (Foundation through Jira Integration Foundation). Before starting:

1. The main solution builds: `dotnet build backend/src`
2. The database exists with all previous migrations applied
3. At least one `GitProviderConnection` and at least one tracked `Repository` with a semver latest tag exist
4. The Jira connection is configured and `IJiraService` can reach the Jira Cloud instance

---

## Step 1: Apply the Migration

```powershell
dotnet ef migrations add AddRepoJiraComparisonSnapshot `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api

dotnet ef database update `
  --project backend/src/RepoManager.Infrastructure `
  --startup-project backend/src/RepoManager.Api
```

This creates `RepoJiraComparisonSnapshots` and adds `LastViewedAt` to `Repositories`. No data backfill required.

---

## Step 2: Write Domain Unit Tests First (TDD)

Write the `SemVer` tests before implementing the value object. Tests must fail initially (Red), then pass (Green).

Required test cases in `RepoManager.UnitTests/Domain/SemVerTests.cs`:

- `TryParse("1.30.0")` → `true`, `ToString() = "1.30.0"`, `NextMinor() = "1.31.0"`
- `TryParse("v2.5.7")` → `true`, `ToString() = "2.5.7"`, `NextMinor() = "2.6.0"`
- `TryParse("0.9.0")` → `true`, `NextMinor() = "0.10.0"` (double-digit minor)
- `TryParse("release-2026")` → `false`
- `TryParse("1.0.0-beta.1")` → `false`
- `TryParse("")` → `false`
- `TryParse(null)` → `false`

```powershell
dotnet test backend/tests/RepoManager.UnitTests `
  --filter "FullyQualifiedName~SemVerTests"
```

---

## Step 3: Extend `IJiraService`

Add the three new methods to the interface and implement them in `JiraService.cs`:

1. `GetTicketsInFixVersionAsync` — JQL: `project IN (P1,P2) AND fixVersion = "<name>"` via `/rest/api/3/search`
2. `AddTicketToFixVersionAsync` — `PUT /rest/api/3/issue/{key}` with `update: { fixVersions: [{ add: { name: "<name>" } }] }`
3. `CreateFixVersionAsync` — `POST /rest/api/3/version` with `{ name, projectId }`

All three use the existing Polly-wrapped `HttpClient` registered for Jira.

---

## Step 4: Implement `RepoJiraComparisonService`

Implement the algorithm in `RepoManager.Infrastructure/Jira/RepoJiraComparisonService.cs`:

1. Resolve latest tag from `Repository.LatestTag` (set by feature 002)
2. Call `SemVer.TryParse`; if false → return `RepoJiraComparisonDto.Unsupported(...)`
3. Compute `nextVersion = current.NextMinor()`, `fixVersionName = $"{repo.Name}_{nextVersion}"`
4. Call `IGitProvider.GetCommitsSinceTagAsync(repo, currentTag, ct)` → extract ticket IDs via `IConventionalCommitParser`
5. Call `IJiraService.GetTicketsInFixVersionAsync(jiraProjectKeys, fixVersionName, ct)` → ticket list
6. Compute set buckets (in both / Jira only / Git only), match rate, health band
7. Serialize buckets to JSON (`System.Text.Json.JsonSerializer.Serialize(...)`)
8. Upsert `RepoJiraComparisonSnapshot` (update if exists, insert if not)
9. Set `Repository.LastViewedAt = DateTime.UtcNow`
10. Call `SaveChangesAsync`, map entity to `RepoJiraComparisonDto`, return

**Cache read path** (before step 2): if `forceRefresh = false` and snapshot exists with `LastSyncedAt > now - 5 min`, deserialize JSON columns and return DTO directly — skip Git and Jira API calls entirely.

---

## Step 5: Register the Service and Background Job

In `RepoManager.Api/Program.cs`:

```csharp
builder.Services.AddScoped<IRepoJiraComparisonService, RepoJiraComparisonService>();
builder.Services.AddHostedService<JiraCoverageRefreshService>();
```

---

## Step 6: Add API Endpoints

Add `JiraCoverageController.cs` in `RepoManager.Api/Controllers/`:

```csharp
[ApiController]
[Route("api/v1")]
[Authorize]
public class JiraCoverageController : ControllerBase
{
    [HttpGet("repositories/{id:int}/jira-coverage")]
    public Task<IActionResult> GetForRepo(int id, [FromQuery] bool refresh = false, CancellationToken ct = default);

    [HttpGet("projects/{id:int}/jira-coverage")]
    public Task<IActionResult> GetForProject(int id, [FromQuery] bool refresh = false, CancellationToken ct = default);

    [HttpPost("repositories/{id:int}/jira-coverage/add-ticket")]
    [Authorize(Roles = "Admin")]
    public Task<IActionResult> AddTicket(int id, [FromBody] AddTicketRequest body, CancellationToken ct = default);
}
```

---

## Step 7: Verify Cache Invalidation Hooks

Ensure the following existing operations set `LastSyncedAt = DateTime.MinValue` on any snapshot for the affected repo:

- `CommitSyncService.SyncAsync(repoId, ...)` — after saving new commits
- `RepositoryService.SetLatestTagAsync(repoId, ...)` — after pinning a new tag

In each service, after the primary write:
```csharp
var snapshot = await _db.RepoJiraComparisonSnapshots
    .Where(s => s.RepositoryId == repoId)
    .ToListAsync(ct);
snapshot.ForEach(s => s.LastSyncedAt = DateTime.MinValue);
await _db.SaveChangesAsync(ct);
```

---

## Step 8: Regenerate Frontend API Client

```powershell
cd frontend
npm run codegen
```

Verify `src/lib/api.d.ts` contains `getRepositoriesIdJiraCoverage`, `getProjectsIdJiraCoverage`, and `postRepositoriesIdJiraCoverageAddTicket`.

---

## Step 9: Implement Frontend (in order)

1. `HealthPill.tsx` — `{ matchRate, health }` → coloured `Badge` (green/amber/red/unknown)
2. `useJiraCoverage.ts` — TanStack Query hooks wrapping the three endpoints
3. `BucketList.tsx` — collapsible bucket renderer (in both / Jira only / Git only); each row shows ticket key + link, summary, status badge, avatar
4. `RepoCoverageCard.tsx` — project page card; receives `RepoJiraComparisonDto`; shows counters + HealthPill + re-sync icon
5. `ProjectCoverageAggregate.tsx` — header strip above the cards; receives `ProjectJiraCoverageDto`
6. `RepoCoverageTab.tsx` — full tab on the service page; composes header strip + summary cards + `BucketList` + unmatched commits panel
7. Wire `RepoCoverageCard` into the project detail page below existing content, sorted by match rate
8. Add `Jira coverage` tab to the repository detail page tab strip using `RepoCoverageTab`

shadcn primitives: `Card`, `Tabs`, `Badge`, `Tooltip`, `Collapsible`, `Skeleton` (loading), `Button`, `AlertDialog` (for add-to-fix-version confirmation).

---

## Step 10: Verify End-to-End

Manual smoke test:

1. Log in as Admin → navigate to a project with multiple repos → coverage cards appear sorted worst-first
2. Hover re-sync icon → tooltip shows last-synced timestamp
3. Click "View details" → service page opens on "Jira coverage" tab
4. Tab shows header strip, four summary cards, three-bucket breakdown, unmatched commits
5. Click "Add to fix version" on a Git-only ticket → confirmation dialog shows fix version name → confirm → ticket moves to "In both" bucket
6. Click "Re-sync" → loading state → data refreshes and `lastSyncedAt` updates
7. Log in as Viewer → same pages → all data visible, no "Re-sync" button, no "Add to fix version" actions
8. Navigate to a repo with a non-semver latest tag → card shows warning pill, no coverage data

---

## Key Implementation Notes

**Match rate formula**:
```
matchRate = inBoth.Count / (jiraKeys.Union(gitTicketKeys)).Count
// Edge case: if union is empty (zero tickets on both sides), matchRate = 1.0 (perfect, nothing to compare)
```

**HealthBand mapping**:
```csharp
HealthBand ComputeHealth(decimal matchRate, bool supported) =>
    !supported ? HealthBand.Unknown :
    matchRate >= 0.90m ? HealthBand.Green :
    matchRate >= 0.60m ? HealthBand.Amber :
    HealthBand.Red;
```

**TanStack Query invalidation after add-ticket**:
```typescript
onSuccess: () => {
  queryClient.invalidateQueries({ queryKey: ['jira-coverage', 'repo', repositoryId] });
  queryClient.invalidateQueries({ queryKey: ['jira-coverage', 'project', projectId] });
}
```

**Structured telemetry events** (Serilog):
```
jira_coverage.computed      — { repoId, durationMs, gitTicketCount, jiraTicketCount, matchRate }
jira_coverage.cache_hit     — { repoId, ageSeconds }
jira_coverage.cache_miss    — { repoId, reason }
jira_coverage.add_to_fix_version — { repoId, ticketKey, userId, fixVersionName, fixVersionCreated, success }
jira_coverage.compute_failed     — { repoId, exceptionType, message }
jira_coverage.background_refresh — { repoCount, successCount, failCount, durationMs }
```
