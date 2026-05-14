using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Commits;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.GitProviders;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Commits;

public class CommitSyncService
{
    private readonly AppDbContext _db;
    private readonly IGitProviderFactory _providerFactory;
    private readonly IDataProtector _protector;
    private readonly IConventionalCommitParser _parser;
    private readonly ILogger<CommitSyncService> _logger;

    public CommitSyncService(
        AppDbContext db,
        IGitProviderFactory providerFactory,
        IDataProtectionProvider dataProtection,
        IConventionalCommitParser parser,
        ILogger<CommitSyncService> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _protector = dataProtection.CreateProtector("GitProviderConnection.Pat");
        _parser = parser;
        _logger = logger;
    }

    public async Task SyncAsync(Guid repositoryId, CancellationToken ct = default)
    {
        var repo = await _db.Repositories
            .Include(r => r.GitProviderConnection)
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct)
            ?? throw new NotFoundException("Repository", repositoryId);

        var connection = repo.GitProviderConnection;
        var pat = _protector.Unprotect(connection.EncryptedPat);
        var providerConn = new ProviderConnection(connection.OrganizationUrl, pat, connection.ProviderType);
        var provider = _providerFactory.GetProvider(connection.ProviderType);

        var tags = await provider.ListTagsAsync(providerConn, repo.ExternalId, ct);
        var latestTag = FindLatestSemverTag(tags);
        var fromRef = latestTag?.Name ?? string.Empty;
        var toRef = "HEAD";

        var commits = await provider.GetCommitsBetweenAsync(providerConn, repo.ExternalId, fromRef, toRef, ct);

        await UpsertCommitsAsync(repositoryId, commits, ct);

        repo.LastSyncedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Commit sync completed for repository {RepositoryId}: {Count} commits processed from '{FromRef}'", repositoryId, commits.Count(), fromRef);

        await AggregateTicketsAsync(repositoryId, fromRef, toRef, ct);
    }

    private async Task UpsertCommitsAsync(
        Guid repositoryId,
        IEnumerable<CommitInfo> commits,
        CancellationToken ct)
    {
        var existing = await _db.Commits
            .Where(c => c.RepositoryId == repositoryId)
            .ToDictionaryAsync(c => c.Sha, ct);

        foreach (var c in commits)
        {
            var parsed = _parser.Parse(c.Message);
            if (existing.TryGetValue(c.Sha, out var row))
            {
                row.Message       = c.Message;
                row.AuthorName    = c.AuthorName;
                row.AuthorEmail   = c.AuthorEmail;
                row.Type          = parsed.Type;
                row.Scope         = parsed.Scope;
                row.Description   = parsed.Description;
                row.IsBreaking    = parsed.IsBreaking;
                row.IsConventional = parsed.IsConventional;
                row.JiraTicketId  = parsed.JiraTicketId;
            }
            else
            {
                _db.Commits.Add(new Commit
                {
                    Id             = Guid.NewGuid(),
                    RepositoryId   = repositoryId,
                    Sha            = c.Sha,
                    ShortSha       = c.Sha[..Math.Min(8, c.Sha.Length)],
                    Message        = c.Message,
                    AuthorName     = c.AuthorName,
                    AuthorEmail    = c.AuthorEmail,
                    CommittedAt    = c.CommittedAt,
                    Type           = parsed.Type,
                    Scope          = parsed.Scope,
                    Description    = parsed.Description,
                    IsBreaking     = parsed.IsBreaking,
                    IsConventional = parsed.IsConventional,
                    JiraTicketId   = parsed.JiraTicketId
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // T052: drop and reinsert ticket projection for the given tag range
    private async Task AggregateTicketsAsync(
        Guid repositoryId, string fromTag, string toTag, CancellationToken ct)
    {
        var commits = await _db.Commits
            .Where(c => c.RepositoryId == repositoryId && c.JiraTicketId != null)
            .ToListAsync(ct);

        var old = await _db.Tickets
            .Where(t => t.RepositoryId == repositoryId && t.FromTag == fromTag && t.ToTag == toTag)
            .ToListAsync(ct);
        _db.Tickets.RemoveRange(old);

        foreach (var group in commits.GroupBy(c => c.JiraTicketId!))
        {
            var g = group.ToList();
            _db.Tickets.Add(new Ticket
            {
                Id               = Guid.NewGuid(),
                TicketId         = group.Key,
                RepositoryId     = repositoryId,
                FromTag          = fromTag,
                ToTag            = toTag,
                PrimaryType      = ComputePrimaryType(g),
                IsBreaking       = g.Any(c => c.IsBreaking),
                CommitCount      = g.Count,
                ContributorCount = g.Select(c => c.AuthorEmail).Distinct().Count(),
                FirstCommittedAt = g.Min(c => c.CommittedAt),
                LastCommittedAt  = g.Max(c => c.CommittedAt)
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private static TagInfo? FindLatestSemverTag(IEnumerable<TagInfo> tags)
    {
        TagInfo? best = null;
        Version? bestVer = null;

        foreach (var tag in tags)
        {
            var raw = tag.Name.TrimStart('v').Split('-')[0];
            if (!Version.TryParse(raw, out var ver)) continue;
            if (bestVer == null || ver > bestVer)
            {
                best = tag;
                bestVer = ver;
            }
        }

        return best;
    }

    private static string? ComputePrimaryType(IList<Commit> commits)
    {
        // Breaking commits take precedence; use that commit's type
        var breaking = commits.FirstOrDefault(c => c.IsBreaking);
        if (breaking != null) return breaking.Type;

        foreach (var priority in new[] { "feat", "fix" })
            if (commits.Any(c => c.Type == priority)) return priority;

        // First non-chore type
        var nonChore = commits.FirstOrDefault(c => c.Type != null && c.Type != "chore");
        if (nonChore != null) return nonChore.Type;

        return commits.Any(c => c.Type == "chore") ? "chore" : null;
    }
}
