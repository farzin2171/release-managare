using RepoManager.Domain.Enums;

namespace RepoManager.Application.GitProviders;

public interface IGitProvider
{
    Task<bool> TestConnectionAsync(ProviderConnection conn, CancellationToken ct = default);
    Task<IEnumerable<RepoSummary>> ListRepositoriesAsync(ProviderConnection conn, CancellationToken ct = default);
    Task<IEnumerable<TagInfo>> ListTagsAsync(ProviderConnection conn, string repoExternalId, CancellationToken ct = default);
    Task<IEnumerable<CommitInfo>> GetCommitsBetweenAsync(ProviderConnection conn, string repoExternalId, string fromRef, string toRef, CancellationToken ct = default);
    Task<IEnumerable<PullRequestInfo>> GetMergedPullRequestsAsync(ProviderConnection conn, string repoExternalId, DateTime since, CancellationToken ct = default);
}

public record ProviderConnection(string OrganizationUrl, string DecryptedPat, ProviderType Type);
public record RepoSummary(string ExternalId, string Name, string DefaultBranch, string WebUrl, string AzureProjectName , Guid AzureProjectId);
public record TagInfo(string Name, string CommitSha, DateTimeOffset? TaggerDate);
public record CommitInfo(string Sha, string Message, string AuthorName, string AuthorEmail, DateTimeOffset CommittedAt);
public record PullRequestInfo(int Id, string Title, string SourceBranch, string TargetBranch, DateTimeOffset MergedAt);
