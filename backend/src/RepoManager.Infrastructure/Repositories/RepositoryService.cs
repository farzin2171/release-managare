using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.GitProviders;
using RepoManager.Application.Repositories;
using RepoManager.Domain.Entities;
using RepoManager.Domain.ValueObjects;
using RepoManager.Infrastructure.Persistence;
using ValidationException = RepoManager.Application.Common.Exceptions.ValidationException;

namespace RepoManager.Infrastructure.Repositories;

public class RepositoryService : IRepositoryService
{
    private readonly AppDbContext _db;
    private readonly IGitProviderService _gitProviderService;
    private readonly ILogger<RepositoryService> _logger;

    public RepositoryService(AppDbContext db, IGitProviderService gitProviderService, ILogger<RepositoryService> logger)
    {
        _db = db;
        _gitProviderService = gitProviderService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RepositoryDto>> ListAsync(ListRepositoriesQuery query, CancellationToken ct = default)
    {
        var q = _db.Repositories.AsQueryable();

        if (query.ConnectionId.HasValue)
            q = q.Where(r => r.GitProviderConnectionId == query.ConnectionId.Value);

        if (query.IsTracked.HasValue)
            q = q.Where(r => r.IsTracked == query.IsTracked.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(r => r.Name.Contains(query.Search));

        var repos = await q.Include(r => r.LatestTagSetBy).OrderBy(r => r.Name).ToListAsync(ct);
        return repos.Select(ToDto).ToList();
    }

    public async Task<RepositoryDto> SetTrackedAsync(Guid id, SetTrackedDto dto, CancellationToken ct = default)
    {
        var repo = await _db.Repositories
            .Include(r => r.LatestTagSetBy)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new NotFoundException("Repository", id);

        repo.IsTracked = dto.IsTracked;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Repository {RepositoryId} tracked status set to {IsTracked}", id, dto.IsTracked);
        return ToDto(repo);
    }

    public async Task<RepositoryChangesDto> GetChangesAsync(
        Guid repositoryId, GetChangesQuery query, CancellationToken ct = default)
    {
        var repo = await _db.Repositories.FindAsync([repositoryId], ct)
            ?? throw new NotFoundException("Repository", repositoryId);

        var tagInfo = await _db.Tickets
            .Where(t => t.RepositoryId == repositoryId)
            .Select(t => new { t.FromTag, t.ToTag })
            .FirstOrDefaultAsync(ct);

        var fromTag = tagInfo?.FromTag ?? string.Empty;
        var toTag   = tagInfo?.ToTag   ?? "HEAD";

        var allCommits = await _db.Commits
            .Where(c => c.RepositoryId == repositoryId)
            .OrderByDescending(c => c.CommittedAt)
            .ToListAsync(ct);

        return query.GroupBy.ToLowerInvariant() switch
        {
            "contributor" => BuildContributorResponse(repo, allCommits, fromTag, toTag, query),
            "commit"      => BuildFlatCommitResponse(repo, allCommits, fromTag, toTag, query),
            _             => await BuildTicketGroupResponse(repo, allCommits, fromTag, toTag, query, ct)
        };
    }

    private async Task<RepositoryChangesDto> BuildTicketGroupResponse(
        Repository repo, List<Commit> allCommits,
        string fromTag, string toTag, GetChangesQuery query, CancellationToken ct)
    {
        var tickets = await _db.Tickets
            .Where(t => t.RepositoryId == repo.Id && t.FromTag == fromTag && t.ToTag == toTag)
            .OrderByDescending(t => t.LastCommittedAt)
            .ToListAsync(ct);

        if (query.Type != null)
            tickets = tickets.Where(t => t.PrimaryType == query.Type).ToList();
        if (query.Search != null)
            tickets = tickets.Where(t => t.TicketId.Contains(query.Search, StringComparison.OrdinalIgnoreCase)).ToList();

        var byTicketId = allCommits.Where(c => c.JiraTicketId != null).ToLookup(c => c.JiraTicketId!);
        var groups = new List<ChangeGroupDto>();

        foreach (var ticket in tickets)
        {
            var ticketCommits = byTicketId[ticket.TicketId].ToList();
            if (query.Contributor != null && !ticketCommits.Any(c => c.AuthorEmail == query.Contributor))
                continue;
            groups.Add(new ChangeGroupDto(
                ticket.TicketId, ticket.Title, ticket.PrimaryType, ticket.IsBreaking,
                ticket.CommitCount, ticket.ContributorCount,
                ticketCommits.Select(ToCommitItemDto).ToList()));
        }

        var unscoped = allCommits.Where(c => c.JiraTicketId == null).Select(ToCommitItemDto).ToList();
        return new RepositoryChangesDto(repo.Id, repo.Name, fromTag, toTag,
            BuildSummary(allCommits, tickets), groups, unscoped);
    }

    private static RepositoryChangesDto BuildContributorResponse(
        Repository repo, List<Commit> allCommits,
        string fromTag, string toTag, GetChangesQuery query)
    {
        var commits = ApplyCommitFilters(allCommits, query);
        var groups = commits
            .GroupBy(c => c.AuthorEmail)
            .OrderByDescending(g => g.Count())
            .Select(g => new ChangeGroupDto(
                g.Key,
                g.First().AuthorName,
                null,
                g.Any(c => c.IsBreaking),
                g.Count(),
                1,
                g.Select(ToCommitItemDto).ToList()))
            .ToList();

        return new RepositoryChangesDto(repo.Id, repo.Name, fromTag, toTag,
            BuildSummary(allCommits, []), groups, []);
    }

    private static RepositoryChangesDto BuildFlatCommitResponse(
        Repository repo, List<Commit> allCommits,
        string fromTag, string toTag, GetChangesQuery query)
    {
        var commits = ApplyCommitFilters(allCommits, query);
        return new RepositoryChangesDto(repo.Id, repo.Name, fromTag, toTag,
            BuildSummary(allCommits, []),
            [],
            commits.Select(ToCommitItemDto).ToList());
    }

    private static List<Commit> ApplyCommitFilters(List<Commit> commits, GetChangesQuery query)
    {
        var q = commits.AsEnumerable();
        if (query.Type != null)        q = q.Where(c => c.Type == query.Type);
        if (query.Contributor != null) q = q.Where(c => c.AuthorEmail == query.Contributor);
        if (query.Search != null)      q = q.Where(c => c.Message.Contains(query.Search, StringComparison.OrdinalIgnoreCase));
        return q.ToList();
    }

    private static ChangeSummaryDto BuildSummary(List<Commit> commits, List<Ticket> tickets)
    {
        return new ChangeSummaryDto(
            CommitCount:      commits.Count,
            TicketCount:      tickets.Count,
            BreakingCount:    commits.Count(c => c.IsBreaking),
            ContributorCount: commits.Select(c => c.AuthorEmail).Distinct().Count());
    }

    public async Task<IReadOnlyList<RepositoryTag>> GetTagsAsync(Guid repositoryId, CancellationToken ct = default)
    {
        var repo = await _db.Repositories.FindAsync([repositoryId], ct)
            ?? throw new NotFoundException("Repository", repositoryId);

        if (!repo.IsTracked)
            throw new ValidationException([new ValidationFailure("", "Repository must be tracked to list tags.")]);

        return await _gitProviderService.ListTagsAsync(repositoryId, ct);
    }

    public async Task<RepositoryDto> SetLatestTagAsync(Guid repositoryId, string tagName, Guid actingUserId, CancellationToken ct = default)
    {
        var repo = await _db.Repositories.FindAsync([repositoryId], ct)
            ?? throw new NotFoundException("Repository", repositoryId);

        if (!repo.IsTracked)
            throw new ValidationException([new ValidationFailure("", "Repository must be tracked to pin a tag.")]);

        var liveTags = await _gitProviderService.ListTagsAsync(repositoryId, ct);
        var tag = liveTags.FirstOrDefault(t => t.Name == tagName)
            ?? throw new ValidationException([new ValidationFailure("tagName", $"Tag '{tagName}' does not exist in the remote repository.")]);

        var oldTag = repo.LatestTag;
        repo.PinLatestTag(tagName, tag.CommitSha, actingUserId, DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "repository.latest_tag.changed repositoryId={RepositoryId} oldTag={OldTag} newTag={NewTag} actingUserId={ActingUserId}",
            repositoryId, oldTag, tagName, actingUserId);

        await InvalidateJiraCoverageSnapshotsAsync(repositoryId, ct);

        await _db.Entry(repo).Reference(r => r.LatestTagSetBy).LoadAsync(ct);
        return ToDto(repo);
    }

    public async Task ClearLatestTagAsync(Guid repositoryId, Guid actingUserId, CancellationToken ct = default)
    {
        var repo = await _db.Repositories.FindAsync([repositoryId], ct)
            ?? throw new NotFoundException("Repository", repositoryId);

        var oldTag = repo.LatestTag;
        repo.ClearLatestTag(actingUserId, DateTime.UtcNow);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "repository.latest_tag.changed repositoryId={RepositoryId} oldTag={OldTag} newTag=null actingUserId={ActingUserId}",
            repositoryId, oldTag, actingUserId);
    }

    private async Task InvalidateJiraCoverageSnapshotsAsync(Guid repositoryId, CancellationToken ct)
    {
        var snapshots = await _db.RepoJiraComparisonSnapshots
            .Where(s => s.RepositoryId == repositoryId)
            .ToListAsync(ct);
        if (snapshots.Count == 0) return;
        snapshots.ForEach(s => s.LastSyncedAt = DateTime.MinValue);
        await _db.SaveChangesAsync(ct);
    }

    private static CommitItemDto ToCommitItemDto(Commit c) =>
        new(c.Sha, c.ShortSha, c.Message, c.AuthorName, c.CommittedAt);

    private static RepositoryDto ToDto(Repository r) =>
        new(r.Id, r.GitProviderConnectionId, r.ExternalId, r.Name,
            r.DefaultBranch, r.WebUrl, r.AzureProjectName, r.IsTracked, r.ServiceOwner,
            r.LastSyncedAt, r.LatestTag, r.LatestTagCommitSha, r.LatestTagSetAt,
            r.LatestTagSetBy is { } u ? new UserSummaryDto(u.Id, u.Email) : null);
}
