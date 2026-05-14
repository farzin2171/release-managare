# Service Interfaces Contract

All interfaces live in `RepoManager.Application`. Implementations live in `RepoManager.Infrastructure`.

---

## IGitProvider

```csharp
// Application/GitProviders/IGitProvider.cs
public interface IGitProvider
{
    Task<bool> TestConnectionAsync(ProviderConnection conn, CancellationToken ct = default);
    Task<IEnumerable<RepoSummary>> ListRepositoriesAsync(ProviderConnection conn, CancellationToken ct = default);
    Task<IEnumerable<TagInfo>> ListTagsAsync(ProviderConnection conn, string repoExternalId, CancellationToken ct = default);
    Task<IEnumerable<CommitInfo>> GetCommitsBetweenAsync(ProviderConnection conn, string repoExternalId, string fromRef, string toRef, CancellationToken ct = default);
    Task<IEnumerable<PullRequestInfo>> GetMergedPullRequestsAsync(ProviderConnection conn, string repoExternalId, DateTime since, CancellationToken ct = default);
}
```

**DTOs**:
```csharp
record ProviderConnection(string OrganizationUrl, string DecryptedPat, ProviderType Type);
record RepoSummary(string ExternalId, string Name, string DefaultBranch, string WebUrl, string AzureProjectName);
record TagInfo(string Name, string CommitSha, DateTimeOffset? TaggerDate);
record CommitInfo(string Sha, string Message, string AuthorName, string AuthorEmail, DateTimeOffset CommittedAt);
record PullRequestInfo(int Id, string Title, string SourceBranch, string TargetBranch, DateTimeOffset MergedAt);
```

## IGitProviderFactory

```csharp
// Application/GitProviders/IGitProviderFactory.cs
public interface IGitProviderFactory
{
    IGitProvider GetProvider(ProviderType providerType);
}
```

**v1 implementation**: `GitProviderFactory` resolves to `AzureDevOpsGitProvider` for `ProviderType.AzureDevOps`.

---

## IConfluencePublisher

```csharp
// Application/Confluence/IConfluencePublisher.cs
public interface IConfluencePublisher
{
    Task<bool> TestConnectionAsync(ConfluenceConnectionDto conn, CancellationToken ct = default);
    Task<PublishResult> CreateOrUpdatePageAsync(ConfluenceConnectionDto conn, string spaceKey, string parentPageId, string title, string markdownContent, string? existingPageId, CancellationToken ct = default);
    Task<PublishResult> CreateChecklistPageAsync(ConfluenceConnectionDto conn, string spaceKey, string parentPageId, string title, string checklistTemplate, CancellationToken ct = default);
}

record PublishResult(bool Success, string? PageId, string? PageUrl, string? ErrorMessage);
record ConfluenceConnectionDto(string BaseUrl, string Username, string DecryptedApiToken);
```

**Idempotency**: If `existingPageId` is provided, `CreateOrUpdatePageAsync` PUTs to update. If null, it POSTs to create. The caller (`ReleaseService`) passes the stored `ConfluencePageId` on retries so the page is updated rather than duplicated (FR-020).

---

## IJiraService

```csharp
// Application/Jira/IJiraService.cs
public interface IJiraService
{
    Task<bool> TestConnectionAsync(JiraConnectionDto conn, CancellationToken ct = default);
    Task<IReadOnlyList<JiraProjectDto>> ListProjectsAsync(Guid connectionId, CancellationToken ct = default);
    Task<JiraReleaseDto> SyncFixVersionAsync(Guid connectionId, string projectKey, string versionName, bool createIfMissing, CancellationToken ct = default);
    Task AddTicketToFixVersionAsync(Guid connectionId, string ticketKey, string versionId, CancellationToken ct = default);
}

record JiraConnectionDto(string BaseUrl, string Username, string DecryptedApiToken);
record JiraProjectDto(string Key, string Name, string ProjectType);
record JiraReleaseDto(string JiraVersionId, string Name, bool IsReleased, DateOnly? ReleaseDate, IReadOnlyList<JiraTicketDto> Tickets);
record JiraTicketDto(string Key, string Summary, string Status, JiraStatusCategory StatusCategory, string IssueType, string? AssigneeName, string? AssigneeEmail, string? Priority, string? ParentKey);
```

**HTTP client registration**:
```csharp
services.AddHttpClient<IJiraService, JiraService>(c =>
    c.BaseAddress = new Uri(jiraBaseUrl))
    .AddPolicyHandler(/* Polly 3-retry exponential on 429 + 5xx */);
```

---

## IConventionalCommitParser

```csharp
// Application/Commits/IConventionalCommitParser.cs
public interface IConventionalCommitParser
{
    ParsedCommit Parse(string commitMessage);
}

record ParsedCommit(
    string? Type,
    string? Scope,
    string? Description,
    bool IsBreaking,
    bool IsConventional,
    string? JiraTicketId   // non-null only if Scope matches ^[A-Z]{2,10}-\d+$
);
```

**Implementation**: `ConventionalCommitParser` in Infrastructure/Commits — pure C# regex, no external deps. **TDD-first mandatory** (Principle III).

---

## IReleaseReconciliationService

```csharp
// Application/Reconciliation/IReleaseReconciliationService.cs
public interface IReleaseReconciliationService
{
    Task<ReconciliationResultDto> ReconcileAsync(Guid releaseId, CancellationToken ct = default);
}

record ReconciliationResultDto(
    Guid ReleaseId,
    DateTimeOffset RunAt,
    int MatchedCount,
    int JiraOnlyCount,
    int GitOnlyCount,
    decimal MatchRatePercent,
    IReadOnlyList<MatchedTicketDto> Matched,
    IReadOnlyList<JiraTicketDto> JiraOnly,
    IReadOnlyList<GitTicketDto> GitOnly
);
```

---

## Application Service Interfaces (one per aggregate)

```csharp
IAuthService         // Login, Refresh, Setup, CreateUser, UpdateUser, DeleteUser, ListUsers
IProjectService      // CreateAsync, ListAsync, GetAsync, UpdateAsync, DeleteAsync,
                     // AssignRepositoryAsync, RemoveRepositoryAsync, ConfigureJiraAsync
IReleaseService      // CreateAsync, GetAsync, UpdateNotesAsync, PublishAsync, ListByProjectAsync
IRepositoryService   // ListAsync, SetTrackedAsync, GetChangesAsync
IGitProviderConnectionService  // CreateAsync, ListAsync, UpdateAsync, TestAsync, SyncAsync
IConfluenceConnectionService   // GetAsync, UpsertAsync, TestAsync
IJiraConnectionService         // GetAsync, UpsertAsync, TestAsync, ListProjectsAsync
IReleaseNoteTemplateService    // CreateAsync, ListAsync, UpdateAsync, DeleteAsync
CommitSyncService              // SyncAsync (called by GitProviderConnectionService.SyncAsync)
IReleaseReconciliationService  // ReconcileAsync (above)
```

All service methods:
- Take `CancellationToken ct = default` as last parameter
- Accept and return DTOs (record types), never EF entities
- Call `await _validator.ValidateAndThrowAsync(dto, ct)` for write DTOs
- Throw typed exceptions: `NotFoundException`, `ConflictException`, `ValidationException`, `ExternalServiceException`

---

## Custom Exceptions

```csharp
// Application/Common/Exceptions/
NotFoundException(string resourceType, object id)    → 404
ConflictException(string message)                    → 409
ValidationException(IEnumerable<ValidationFailure> failures) → 400
ExternalServiceException(string service, string message, Exception? inner) → 502
```

Mapped to RFC 7807 ProblemDetails by `GlobalExceptionHandler` in `RepoManager.Api`.
