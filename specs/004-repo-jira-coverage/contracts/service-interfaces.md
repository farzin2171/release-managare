# Service Interface Contracts: Per-Repo Jira Coverage

**Phase 1 output** — New and extended service interfaces for this feature.

---

## New Interface: `IRepoJiraComparisonService`

Located at `RepoManager.Application/Jira/IRepoJiraComparisonService.cs`.

```csharp
public interface IRepoJiraComparisonService
{
    /// Returns the cached or freshly computed comparison for a single repository.
    /// Updates Repository.LastViewedAt as a side effect.
    Task<RepoJiraComparisonDto> GetForRepoAsync(
        int repositoryId,
        bool forceRefresh,
        CancellationToken ct = default);

    /// Returns coverage for all repositories in a project, plus project aggregate.
    Task<ProjectJiraCoverageDto> GetForProjectAsync(
        int projectId,
        bool forceRefresh,
        CancellationToken ct = default);

    /// Adds the given ticket to the computed Jira fix version; creates the fix version if absent.
    /// Invalidates the snapshot cache for the repository after success.
    Task<AddToFixVersionResultDto> AddTicketToFixVersionAsync(
        int repositoryId,
        string ticketKey,
        CancellationToken ct = default);
}
```

**Implementation class**: `RepoManager.Infrastructure/Jira/RepoJiraComparisonService.cs`

**Injected dependencies**:
- `IGitProvider` (via `IGitProviderFactory`) — `GetCommitsSinceTagAsync`
- `IJiraService` — `GetTicketsInFixVersionAsync`, `AddTicketToFixVersionAsync`, `CreateFixVersionAsync`
- `IConventionalCommitParser` — `ExtractJiraTicket(string commitMessage)`
- `AppDbContext` — snapshot persistence
- `ILogger<RepoJiraComparisonService>`

---

## Extended Interface: `IJiraService`

The following methods are added to the existing `RepoManager.Application/Jira/IJiraService.cs` interface. No existing methods are changed.

```csharp
/// Returns all Jira issues in any of the given projects that have the named fix version.
/// Returns an empty list (not an error) if the fix version does not exist.
Task<IReadOnlyList<JiraIssueSummary>> GetTicketsInFixVersionAsync(
    IEnumerable<string> jiraProjectKeys,
    string fixVersionName,
    CancellationToken ct = default);

/// Adds the given fix version name to the issue's fixVersions field.
/// If the issue already has the fix version, this is a no-op (idempotent).
Task AddTicketToFixVersionAsync(
    string ticketKey,
    string fixVersionName,
    CancellationToken ct = default);

/// Creates a fix version with the given name in the given Jira project.
/// Returns the new fix version's ID.
Task<string> CreateFixVersionAsync(
    string jiraProjectKey,
    string fixVersionName,
    CancellationToken ct = default);
```

**`JiraIssueSummary`** (new DTO, `RepoManager.Application/Jira/Dtos/`):

```csharp
public record JiraIssueSummary(
    string Key,
    string Summary,
    string Status,
    string StatusCategory,
    string? AssigneeAvatarUrl
);
```

---

## New Background Service: `JiraCoverageRefreshService`

Located at `RepoManager.Infrastructure/BackgroundServices/JiraCoverageRefreshService.cs`.

```csharp
public sealed class JiraCoverageRefreshService : BackgroundService
{
    // Runs every 10 minutes.
    // Queries: SELECT r.Id FROM Repositories r
    //          LEFT JOIN RepoJiraComparisonSnapshots s ON s.RepositoryId = r.Id
    //          WHERE r.LastViewedAt > now - 24h
    //            AND (s.LastSyncedAt IS NULL OR s.LastSyncedAt < now - 5min)
    // For each result: calls _comparisonService.GetForRepoAsync(id, forceRefresh: true, ct)
    // Logs: jira_coverage.background_refresh — count, durationMs, errors
}
```

Registered as a hosted service in `RepoManager.Api/Program.cs` via:
```csharp
builder.Services.AddHostedService<JiraCoverageRefreshService>();
```
