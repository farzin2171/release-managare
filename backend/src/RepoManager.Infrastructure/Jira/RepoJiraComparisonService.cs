using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Commits;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.GitProviders;
using RepoManager.Application.Jira;
using RepoManager.Application.Jira.Dtos;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Domain.ValueObjects;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Jira;

public class RepoJiraComparisonService : IRepoJiraComparisonService
{
    private readonly AppDbContext _db;
    private readonly IGitProviderFactory _providerFactory;
    private readonly IJiraService _jiraService;
    private readonly IConventionalCommitParser _parser;
    private readonly IDataProtector _protector;
    private readonly ILogger<RepoJiraComparisonService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public RepoJiraComparisonService(
        AppDbContext db,
        IGitProviderFactory providerFactory,
        IJiraService jiraService,
        IConventionalCommitParser parser,
        IDataProtectionProvider dataProtection,
        ILogger<RepoJiraComparisonService> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _jiraService = jiraService;
        _parser = parser;
        _protector = dataProtection.CreateProtector("GitProviderConnection.Pat");
        _logger = logger;
    }

    public async Task<RepoJiraComparisonDto> GetForRepoAsync(
        Guid repositoryId,
        bool forceRefresh,
        CancellationToken ct = default)
    {
        var repo = await _db.Repositories
            .Include(r => r.GitProviderConnection)
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct)
            ?? throw new NotFoundException("Repository", repositoryId);

        var now = DateTime.UtcNow;

        if (!SemVer.TryParse(repo.LatestTag ?? string.Empty, out var semVer) || semVer == null)
        {
            var unsupSnap = await _db.RepoJiraComparisonSnapshots
                .FirstOrDefaultAsync(s => s.RepositoryId == repositoryId && !s.Supported, ct);

            if (!forceRefresh && unsupSnap != null && unsupSnap.LastSyncedAt > now.AddMinutes(-5))
            {
                repo.LastViewedAt = now;
                await _db.SaveChangesAsync(ct);
                return MapToDto(repo, unsupSnap);
            }

            return await PersistUnsupportedAsync(repo, unsupSnap, now, ct);
        }

        var nextVersionStr = semVer.NextMinor().ToString();
        var snapshot = await _db.RepoJiraComparisonSnapshots
            .FirstOrDefaultAsync(s => s.RepositoryId == repositoryId && s.NextVersion == nextVersionStr, ct);

        if (!forceRefresh && snapshot != null && snapshot.LastSyncedAt > now.AddMinutes(-5))
        {
            _logger.LogInformation("jira_coverage.cache_hit repoId={RepositoryId} ageSeconds={AgeSeconds}",
                repositoryId, (now - snapshot.LastSyncedAt).TotalSeconds);
            repo.LastViewedAt = now;
            await _db.SaveChangesAsync(ct);
            return MapToDto(repo, snapshot);
        }

        _logger.LogInformation("jira_coverage.cache_miss repoId={RepositoryId} reason={Reason}",
            repositoryId, snapshot == null ? "no_snapshot" : "stale");

        try
        {
            return await ComputeAndPersistAsync(repo, snapshot, semVer, now, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "jira_coverage.compute_failed repoId={RepositoryId} exceptionType={ExType} message={Message}",
                repositoryId, ex.GetType().Name, ex.Message);
            throw;
        }
    }

    public async Task<ProjectJiraCoverageDto> GetForProjectAsync(
        Guid projectId,
        bool forceRefresh,
        CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.ProjectRepositories)
                .ThenInclude(pr => pr.Repository)
                    .ThenInclude(r => r!.GitProviderConnection)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new NotFoundException("Project", projectId);

        var now = DateTime.UtcNow;
        var repoIds = project.ProjectRepositories.Select(pr => pr.RepositoryId).ToList();

        var snapshots = await _db.RepoJiraComparisonSnapshots
            .Where(s => repoIds.Contains(s.RepositoryId))
            .ToListAsync(ct);

        var snapshotsByRepo = snapshots
            .GroupBy(s => s.RepositoryId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.LastSyncedAt).First());

        var repoDtos = new List<RepoJiraComparisonDto>();
        foreach (var pr in project.ProjectRepositories)
        {
            var repo = pr.Repository;
            snapshotsByRepo.TryGetValue(repo.Id, out var snap);
            bool hasFreshSnap = snap != null && snap.LastSyncedAt > now.AddMinutes(-5);

            if (forceRefresh || hasFreshSnap)
            {
                try
                {
                    repoDtos.Add(await GetForRepoAsync(repo.Id, forceRefresh, ct));
                }
                catch
                {
                    repoDtos.Add(CreateSentinelDto(repo));
                }
            }
            else
            {
                repoDtos.Add(CreateSentinelDto(repo));
            }
        }

        var supportedRepos = repoDtos.Where(r => r.Supported).ToList();
        var greenCount = supportedRepos.Count(r => r.Health == HealthBand.Green);
        var attentionCount = supportedRepos.Count(r => r.Health is HealthBand.Amber or HealthBand.Red);

        var totalUnion = supportedRepos.Sum(r =>
            r.Counts.GitTicketCount + r.Counts.JiraTicketCount - r.Counts.InBothCount);
        var projectMatchRate = totalUnion == 0
            ? (supportedRepos.Count > 0 ? 1m : 0m)
            : supportedRepos.Sum(r =>
                r.MatchRate * (r.Counts.GitTicketCount + r.Counts.JiraTicketCount - r.Counts.InBothCount))
              / totalUnion;

        var sorted = repoDtos
            .OrderBy(r => r.Supported ? 0 : 1)
            .ThenBy(r => r.Supported ? (double)r.MatchRate : double.MaxValue)
            .ThenBy(r => r.RepositoryName)
            .ToList();

        return new ProjectJiraCoverageDto(
            project.Id, project.Name,
            repoDtos.Count, greenCount, attentionCount,
            Math.Round(projectMatchRate, 4),
            sorted);
    }

    public async Task<AddToFixVersionResultDto> AddTicketToFixVersionAsync(
        Guid repositoryId,
        string ticketKey,
        CancellationToken ct = default)
    {
        var repo = await _db.Repositories
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct)
            ?? throw new NotFoundException("Repository", repositoryId);

        var snapshot = await _db.RepoJiraComparisonSnapshots
            .Where(s => s.RepositoryId == repositoryId && s.Supported)
            .OrderByDescending(s => s.LastSyncedAt)
            .FirstOrDefaultAsync(ct)
            ?? throw new ConflictException(
                $"Repository '{repo.Name}' has no valid SemVer tag; fix version cannot be determined.");

        var fixVersionName = snapshot.JiraFixVersionName;
        var dashIndex = ticketKey.IndexOf('-');
        var projectKey = dashIndex > 0 ? ticketKey[..dashIndex] : ticketKey;

        bool fixVersionCreated;
        try
        {
            await _jiraService.CreateFixVersionAsync(projectKey, fixVersionName, ct);
            fixVersionCreated = true;
        }
        catch (ExternalServiceException)
        {
            fixVersionCreated = false;
        }

        await _jiraService.AddTicketToFixVersionAsync(ticketKey, fixVersionName, ct);

        var snapshots = await _db.RepoJiraComparisonSnapshots
            .Where(s => s.RepositoryId == repositoryId)
            .ToListAsync(ct);
        snapshots.ForEach(s => s.LastSyncedAt = DateTime.MinValue);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "jira_coverage.add_ticket repoId={RepositoryId} ticketKey={TicketKey} fixVersionName={FixVersionName} fixVersionCreated={FixVersionCreated}",
            repositoryId, ticketKey, fixVersionName, fixVersionCreated);

        return new AddToFixVersionResultDto(true, fixVersionName, fixVersionCreated);
    }

    private async Task<RepoJiraComparisonDto> PersistUnsupportedAsync(
        Repository repo,
        RepoJiraComparisonSnapshot? existing,
        DateTime now,
        CancellationToken ct)
    {
        var reason = string.IsNullOrWhiteSpace(repo.LatestTag)
            ? "No latest tag is set for this repository."
            : $"Latest tag '{repo.LatestTag}' is not a semver tag (MAJOR.MINOR.PATCH).";

        var snap = existing ?? new RepoJiraComparisonSnapshot { RepositoryId = repo.Id };
        snap.CurrentTag = repo.LatestTag ?? string.Empty;
        snap.NextVersion = string.Empty;
        snap.JiraFixVersionName = string.Empty;
        snap.JiraFixVersionExists = false;
        snap.Supported = false;
        snap.UnsupportedReason = reason;
        snap.CommitCount = 0;
        snap.GitTicketCount = 0;
        snap.JiraTicketCount = 0;
        snap.InBothCount = 0;
        snap.JiraOnlyCount = 0;
        snap.GitOnlyCount = 0;
        snap.MatchRate = 0m;
        snap.InBothJson = "[]";
        snap.JiraOnlyJson = "[]";
        snap.GitOnlyJson = "[]";
        snap.UnmatchedCommitsJson = "[]";
        snap.LastSyncedAt = now;
        snap.LastSyncError = null;

        if (existing == null)
            _db.RepoJiraComparisonSnapshots.Add(snap);

        repo.LastViewedAt = now;
        await _db.SaveChangesAsync(ct);
        return MapToDto(repo, snap);
    }

    private async Task<RepoJiraComparisonDto> ComputeAndPersistAsync(
        Repository repo,
        RepoJiraComparisonSnapshot? existing,
        SemVer semVer,
        DateTime now,
        CancellationToken ct)
    {
        var nextVersion = semVer.NextMinor();
        var fixVersionName = $"{repo.Name}_{nextVersion}";
        var jiraProjectKeys = await GetJiraProjectKeysForRepoAsync(repo.Id, ct);

        var sw = Stopwatch.StartNew();

        var pat = _protector.Unprotect(repo.GitProviderConnection.EncryptedPat);
        var providerConn = new ProviderConnection(
            repo.GitProviderConnection.OrganizationUrl, pat, repo.GitProviderConnection.ProviderType);
        var provider = _providerFactory.GetProvider(repo.GitProviderConnection.ProviderType);
        
        DateTimeOffset? fromDate = null;
        if (!string.IsNullOrEmpty(repo.LatestTagCommitSha))
            fromDate = await provider.GetCommitDateAsync(providerConn, repo.ExternalId, repo.LatestTagCommitSha, ct);

        var rawCommits = (await provider.GetCommitsBetweenAsync(
            providerConn, repo.ExternalId, repo.LatestTag!, repo.DefaultBranch,fromDate, ct: ct)).ToList();

        var parsedCommits = rawCommits
            .Select(c => (commit: c, parsed: _parser.Parse(c.Message)))
            .ToList();

        var gitTicketKeys = parsedCommits
            .Where(x => x.parsed.JiraTicketId != null)
            .Select(x => x.parsed.JiraTicketId!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        IReadOnlyList<JiraIssueSummary> jiraTickets = await _jiraService.GetTicketsInFixVersionAsync(jiraProjectKeys, fixVersionName, ct);
            

        var jiraKeys = jiraTickets.Select(t => t.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var commitCountByKey = parsedCommits
            .Where(x => x.parsed.JiraTicketId != null)
            .GroupBy(x => x.parsed.JiraTicketId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var inBoth = jiraTickets
            .Where(t => gitTicketKeys.Contains(t.Key))
            .Select(t => ToTicketSummary(t, commitCountByKey.GetValueOrDefault(t.Key, 0)))
            .ToList();

        var jiraOnly = jiraTickets
            .Where(t => !gitTicketKeys.Contains(t.Key))
            .Select(t => ToTicketSummary(t, 0))
            .ToList();

        var gitOnly = gitTicketKeys
            .Where(k => !jiraKeys.Contains(k))
            .Select(k => new TicketSummaryDto(k, null, null, null, null,
                commitCountByKey.GetValueOrDefault(k, 0)))
            .ToList();

        var unmatchedCommits = parsedCommits
            .Where(x => x.parsed.JiraTicketId == null)
            .Select(x => new CommitSummaryDto(x.commit.Sha, x.commit.AuthorName, x.commit.Message))
            .ToList();

        var unionCount = jiraKeys.Union(gitTicketKeys, StringComparer.OrdinalIgnoreCase).Count();
        var matchRate = unionCount == 0 ? 1.0m : (decimal)inBoth.Count / unionCount;

        sw.Stop();
        _logger.LogInformation(
            "jira_coverage.computed repoId={RepositoryId} durationMs={DurationMs} gitTicketCount={GitCount} jiraTicketCount={JiraCount} matchRate={MatchRate}",
            repo.Id, sw.ElapsedMilliseconds, gitTicketKeys.Count, jiraTickets.Count, matchRate);

        var snap = existing ?? new RepoJiraComparisonSnapshot { RepositoryId = repo.Id };
        snap.CurrentTag = repo.LatestTag!;
        snap.NextVersion = nextVersion.ToString();
        snap.JiraFixVersionName = fixVersionName;
        snap.JiraFixVersionExists = jiraTickets.Count > 0;
        snap.Supported = true;
        snap.UnsupportedReason = null;
        snap.CommitCount = rawCommits.Count;
        snap.GitTicketCount = gitTicketKeys.Count;
        snap.JiraTicketCount = jiraTickets.Count;
        snap.InBothCount = inBoth.Count;
        snap.JiraOnlyCount = jiraOnly.Count;
        snap.GitOnlyCount = gitOnly.Count;
        snap.MatchRate = matchRate;
        snap.InBothJson = JsonSerializer.Serialize(inBoth, JsonOptions);
        snap.JiraOnlyJson = JsonSerializer.Serialize(jiraOnly, JsonOptions);
        snap.GitOnlyJson = JsonSerializer.Serialize(gitOnly, JsonOptions);
        snap.UnmatchedCommitsJson = JsonSerializer.Serialize(unmatchedCommits, JsonOptions);
        snap.LastSyncedAt = now;
        snap.LastSyncError = null;

        if (existing == null)
            _db.RepoJiraComparisonSnapshots.Add(snap);

        repo.LastViewedAt = now;
        await _db.SaveChangesAsync(ct);
        return MapToDto(repo, snap);
    }

    private async Task<List<string>> GetJiraProjectKeysForRepoAsync(
        Guid repositoryId, CancellationToken ct)
    {
        var keyJsons = await _db.Projects
            .Where(p => p.ProjectRepositories.Any(pr => pr.RepositoryId == repositoryId))
            .Select(p => p.JiraProjectKeys)
            .ToListAsync(ct);

        return keyJsons
            .SelectMany(ParseJiraProjectKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParseJiraProjectKeys(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return [];
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static RepoJiraComparisonDto CreateSentinelDto(Repository repo) =>
        new(repo.Id, repo.Name, repo.LatestTag, null, null, false, false,
            "Coverage not yet computed. Loading…",
            new ComparisonCounts(0, 0, 0, 0, 0, 0),
            0m, HealthBand.Unknown, [], [], [], [], DateTime.MinValue);

    private static RepoJiraComparisonDto MapToDto(Repository repo, RepoJiraComparisonSnapshot snap) =>
        new(repo.Id, repo.Name,
            snap.CurrentTag.Length > 0 ? snap.CurrentTag : null,
            snap.NextVersion.Length > 0 ? snap.NextVersion : null,
            snap.JiraFixVersionName.Length > 0 ? snap.JiraFixVersionName : null,
            snap.JiraFixVersionExists, snap.Supported, snap.UnsupportedReason,
            new ComparisonCounts(snap.CommitCount, snap.GitTicketCount, snap.JiraTicketCount,
                snap.InBothCount, snap.JiraOnlyCount, snap.GitOnlyCount),
            snap.MatchRate,
            ComputeHealthBand(snap.MatchRate, snap.Supported),
            DeserializeList<TicketSummaryDto>(snap.InBothJson),
            DeserializeList<TicketSummaryDto>(snap.JiraOnlyJson),
            DeserializeList<TicketSummaryDto>(snap.GitOnlyJson),
            DeserializeList<CommitSummaryDto>(snap.UnmatchedCommitsJson),
            snap.LastSyncedAt);

    private static IReadOnlyList<T> DeserializeList<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return [];
        try { return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? []; }
        catch { return []; }
    }

    private static TicketSummaryDto ToTicketSummary(JiraIssueSummary issue, int commitCount) =>
        new(issue.Key, issue.Summary, issue.Status, issue.StatusCategory,
            issue.AssigneeAvatarUrl, commitCount);

    private static HealthBand ComputeHealthBand(decimal matchRate, bool supported) =>
        !supported ? HealthBand.Unknown :
        matchRate >= 0.90m ? HealthBand.Green :
        matchRate >= 0.60m ? HealthBand.Amber :
        HealthBand.Red;
}
