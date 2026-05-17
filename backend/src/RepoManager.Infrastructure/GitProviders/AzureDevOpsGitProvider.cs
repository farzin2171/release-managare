using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.GitProviders;

namespace RepoManager.Infrastructure.GitProviders;

public class AzureDevOpsGitProvider : IGitProvider
{
    private readonly ILogger<AzureDevOpsGitProvider> _logger;

    public AzureDevOpsGitProvider(ILogger<AzureDevOpsGitProvider> logger) => _logger = logger;

    private static GitHttpClient CreateGitClient(ProviderConnection conn)
    {
        var credentials = new VssBasicCredential(string.Empty, conn.DecryptedPat);
        var connection = new VssConnection(new Uri(conn.OrganizationUrl), credentials);
        return connection.GetClient<GitHttpClient>();
    }

    public async Task<bool> TestConnectionAsync(ProviderConnection conn, CancellationToken ct = default)
    {
        try
        {
            var client = CreateGitClient(conn);
            await client.GetRepositoriesAsync(cancellationToken: ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IEnumerable<RepoSummary>> ListRepositoriesAsync(ProviderConnection conn, CancellationToken ct = default)
    {
        try
        {
            var client = CreateGitClient(conn);
            var repos = await client.GetRepositoriesAsync(cancellationToken: ct);
            var repoSummaries = repos.Select(r => new RepoSummary(
                r.Id.ToString(),
                r.Name,
                r.DefaultBranch?.Replace("refs/heads/", "") ?? "main",
                r.RemoteUrl ?? string.Empty,
                r.ProjectReference?.Name ?? string.Empty,
                r.ProjectReference?.Id ?? Guid.Empty));

            // Filter to only include repos from the specified Azure DevOps project
            // Todo: will need to add configuration to specify which project(s) to include, rather than hardcoding the project ID
            var filteredRepos = repoSummaries.Where(r => r.AzureProjectId == Guid.Parse("7b9dd9ad-2823-4d24-b1e5-6d0b0f3c4601"));

            return filteredRepos;
        }
        catch (Exception ex)
        {
            throw new ExternalServiceException("AzureDevOps", "Failed to list repositories.", ex);
        }
    }

    public async Task<IEnumerable<TagInfo>> ListTagsAsync(ProviderConnection conn, string repoExternalId, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = CreateGitClient(conn);
            var refs = await client.GetRefsAsync(
                repositoryId: repoExternalId,
                project: null,
                filter: "tags/",
                includeLinks: null,
                includeStatuses: null,
                includeMyBranches: null,
                latestStatusesOnly: null,
                peelTags: true,
                filterContains: null,
                userState: null,
                cancellationToken: ct);

            var tagRefs = refs.Take(200).ToList();

            var commitTasks = tagRefs.Select(async r =>
            {
                var commitSha = r.PeeledObjectId ?? r.ObjectId;
                try
                {
                    var commit = await client.GetCommitAsync(commitSha, repoExternalId, cancellationToken: ct);
                    return new TagInfo(
                        r.Name.Replace("refs/tags/", ""),
                        commitSha,
                        commit.Author?.Date is DateTime d ? new DateTimeOffset(d, TimeSpan.Zero) : (DateTimeOffset?)null,
                        commit.Author?.Name);
                }
                catch
                {
                    return new TagInfo(r.Name.Replace("refs/tags/", ""), commitSha, null, null);
                }
            });

            var result = await Task.WhenAll(commitTasks);

            _logger.LogInformation(
                "repository.tags.fetched repositoryId={RepositoryId} count={Count} durationMs={DurationMs} outcome=success",
                repoExternalId, result.Length, sw.ElapsedMilliseconds);

            return result;
        }
        catch (ExternalServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "repository.tags.fetched repositoryId={RepositoryId} durationMs={DurationMs} outcome=error",
                repoExternalId, sw.ElapsedMilliseconds);
            throw new ExternalServiceException("AzureDevOps", $"Failed to list tags for repository '{repoExternalId}'.", ex);
        }
    }

    public async Task<DateTimeOffset?> GetCommitDateAsync(
        ProviderConnection conn, string repoExternalId, string commitSha, CancellationToken ct = default)
    {
        try
        {
            var client = CreateGitClient(conn);
            var commit = await client.GetCommitAsync(commitSha, repoExternalId, cancellationToken: ct);
            return commit.Author?.Date is DateTime d ? new DateTimeOffset(d, TimeSpan.Zero) : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<IEnumerable<CommitInfo>> GetCommitsBetweenAsync(
        ProviderConnection conn, string repoExternalId, string fromRef, string toRef,
        DateTimeOffset? fromDate = null, CancellationToken ct = default)
    {
        try
        {
            var client = CreateGitClient(conn);
            var repoId = Guid.Parse(repoExternalId);

            var criteria = new GitQueryCommitsCriteria
            {
                ItemVersion = BuildVersionDescriptor(toRef, GitVersionType.Branch),
            };

            if (fromDate.HasValue)
                criteria.FromDate = fromDate.Value.AddSeconds(1).ToString("o");
            else if (!string.IsNullOrEmpty(fromRef))
                criteria.CompareVersion = BuildVersionDescriptor(fromRef, GitVersionType.Tag);

            var allCommits = new List<GitCommitRef>();
            int skip = 0;
            const int pageSize = 1000;

            while (true)
            {
                var page = await client.GetCommitsAsync(
                    repositoryId: repoId.ToString(),
                    searchCriteria: criteria,
                    project: null,
                    skip: skip,
                    top: pageSize,
                    cancellationToken: ct);

                if (page.Count == 0) break;
                allCommits.AddRange(page);
                if (page.Count < pageSize) break;
                skip += page.Count;
            }

            return allCommits.Select(c => new CommitInfo(
                c.CommitId,
                c.Comment,
                c.Author?.Name ?? string.Empty,
                c.Author?.Email ?? string.Empty,
                c.Author?.Date is DateTime d
                    ? new DateTimeOffset(d, TimeSpan.Zero)
                    : DateTimeOffset.UtcNow));
        }
        catch (ExternalServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExternalServiceException("AzureDevOps", $"Failed to get commits for repository '{repoExternalId}'.", ex);
        }
    }

    public async Task<IEnumerable<PullRequestInfo>> GetMergedPullRequestsAsync(
        ProviderConnection conn, string repoExternalId, DateTime since, CancellationToken ct = default)
    {
        try
        {
            var client = CreateGitClient(conn);
            var repoId = Guid.Parse(repoExternalId);

            var criteria = new GitPullRequestSearchCriteria
            {
                Status = PullRequestStatus.Completed
            };

            var prs = await client.GetPullRequestsAsync(
                repositoryId: repoId.ToString(),
                searchCriteria: criteria,
                project: null,
                cancellationToken: ct);

            return prs
                .Where(pr => pr.ClosedDate >= since)
                .Select(pr => new PullRequestInfo(
                    pr.PullRequestId,
                    pr.Title,
                    pr.SourceRefName,
                    pr.TargetRefName,
                    pr.ClosedDate != default
                        ? new DateTimeOffset(pr.ClosedDate, TimeSpan.Zero)
                        : DateTimeOffset.UtcNow));
        }
        catch (ExternalServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ExternalServiceException("AzureDevOps", $"Failed to get pull requests for repository '{repoExternalId}'.", ex);
        }
    }

    private static GitVersionDescriptor? BuildVersionDescriptor(string refName, GitVersionType fallbackType = GitVersionType.Tag)
    {
        if (string.IsNullOrEmpty(refName) || refName == "HEAD")
            return null;

        if (refName.Length == 40 && refName.All(c => "0123456789abcdefABCDEF".Contains(c)))
            return new GitVersionDescriptor { Version = refName, VersionType = GitVersionType.Commit };

        return new GitVersionDescriptor { Version = refName, VersionType = fallbackType };
    }
}
