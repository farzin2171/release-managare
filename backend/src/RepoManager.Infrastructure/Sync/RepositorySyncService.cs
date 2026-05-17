using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Commits;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.DTOs;
using RepoManager.Application.Events;
using RepoManager.Application.GitProviders;
using RepoManager.Application.Queues;
using RepoManager.Application.Services;
using RepoManager.Domain.Aggregates;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Domain.ValueObjects;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Sync;

public class RepositorySyncService : IRepositorySyncService
{
    private const int CommitCap = 5_000;

    private readonly AppDbContext _db;
    private readonly IGitProviderFactory _providerFactory;
    private readonly IDataProtector _protector;
    private readonly IConventionalCommitParser _parser;
    private readonly ISyncJobQueue _queue;
    private readonly ISyncEventPublisher _events;
    private readonly IProjectSyncSnapshotService _snapshot;
    private readonly ILogger<RepositorySyncService> _logger;

    public RepositorySyncService(
        AppDbContext db,
        IGitProviderFactory providerFactory,
        IDataProtectionProvider dataProtection,
        IConventionalCommitParser parser,
        ISyncJobQueue queue,
        ISyncEventPublisher events,
        IProjectSyncSnapshotService snapshot,
        ILogger<RepositorySyncService> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _protector = dataProtection.CreateProtector("GitProviderConnection.Pat");
        _parser = parser;
        _queue = queue;
        _events = events;
        _snapshot = snapshot;
        _logger = logger;
    }

    // ── T018: Enqueue path ──────────────────────────────────────────────────

    public async Task<RepositorySyncDto> EnqueueAsync(Guid repositoryId, Guid userId, CancellationToken ct = default)
    {
        var repo = await _db.Repositories
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct)
            ?? throw new NotFoundException("Repository", repositoryId);

        if (string.IsNullOrEmpty(repo.LatestTag))
            return ToDto(await CreateSkippedRowAsync(repo, userId, ct));

        var activeExists = await _db.RepositorySyncs.AnyAsync(
            s => s.RepositoryId == repositoryId &&
                 (s.Status == SyncStatus.Pending || s.Status == SyncStatus.InProgress), ct);

        if (activeExists)
            throw new ConflictException("A sync is already in progress for this repository.");

        var sync = CreatePendingRow(repo, userId);
        _db.RepositorySyncs.Add(sync);
        await _db.SaveChangesAsync(ct);

        _events.CreateChannel(sync.Id);
        await _queue.EnqueueAsync(new SyncJob(sync.Id, repositoryId), ct);

        return ToDto(sync);
    }

    private async Task<RepositorySync> CreateSkippedRowAsync(Repository repo, Guid userId, CancellationToken ct)
    {
        var sync = new RepositorySync
        {
            Id = Guid.NewGuid(),
            RepositoryId = repo.Id,
            FromTag = string.Empty,
            Status = SyncStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow,
            TriggeredByUserId = userId
        };
        sync.Skip("NoPinnedTag");
        _db.RepositorySyncs.Add(sync);
        await _db.SaveChangesAsync(ct);
        return sync;
    }

    private static RepositorySync CreatePendingRow(Repository repo, Guid userId) => new()
    {
        Id = Guid.NewGuid(),
        RepositoryId = repo.Id,
        FromTag = repo.LatestTag!,
        Status = SyncStatus.Pending,
        StartedAt = DateTimeOffset.UtcNow,
        TriggeredByUserId = userId
    };

    // ── T019: Execute path (called by worker only) ──────────────────────────

    public async Task ExecuteAsync(Guid repositorySyncId, CancellationToken ct = default)
    {
        var sync = await _db.RepositorySyncs
            .Include(s => s.Repository).ThenInclude(r => r.GitProviderConnection)
            .FirstOrDefaultAsync(s => s.Id == repositorySyncId, ct)
            ?? throw new NotFoundException("RepositorySync", repositorySyncId);

        var sw = Stopwatch.StartNew();
        sync.Start();
        await _db.SaveChangesAsync(ct);

        try
        {
            await RunSyncStepsAsync(sync, sw, ct);
        }
        catch (Exception ex)
        {
            sync.Fail(ex.Message[..Math.Min(ex.Message.Length, 1000)]);
            await _db.SaveChangesAsync(ct);
            _logger.LogWarning("Sync {SyncId} for repo {RepoName} failed: {ErrorMessage}",
                sync.Id, sync.Repository.Name, ex.Message);
            _events.CloseChannel(sync.Id);
        }
    }

    private async Task RunSyncStepsAsync(RepositorySync sync, Stopwatch sw, CancellationToken ct)
    {
        var repo = sync.Repository;
        var conn = BuildProviderConnection(repo);
        var provider = _providerFactory.GetProvider(repo.GitProviderConnection.ProviderType);

        await StepAsync(sync, SyncStep.FetchingCommits, sw, ct);

        DateTimeOffset? fromDate = null;
        if (!string.IsNullOrEmpty(repo.LatestTagCommitSha))
            fromDate = await provider.GetCommitDateAsync(conn, repo.ExternalId, repo.LatestTagCommitSha, ct);

        var rawCommits = (await provider.GetCommitsBetweenAsync(conn, repo.ExternalId, sync.FromTag, repo.DefaultBranch, fromDate, ct)).ToList();

        if (rawCommits.Count > CommitCap)
            throw new InvalidOperationException($"Commit count {rawCommits.Count} exceeds the {CommitCap}-commit cap.");

        await StepAsync(sync, SyncStep.ParsingCommits, sw, ct);
        var parsed = rawCommits.Select(c => (commit: c, result: _parser.Parse(c.Message))).ToList();

        await StepAsync(sync, SyncStep.PersistingCommits, sw, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        await UpsertCommitsAsync(repo.Id, parsed, ct);

        await StepAsync(sync, SyncStep.AggregatingTickets, sw, ct);
        var ticketCount = await AggregateTicketsAsync(repo.Id, sync.FromTag, ct);

        await StepAsync(sync, SyncStep.Finalising, sw, ct);
        var contributors = BuildContributors(rawCommits);

        var headSha = rawCommits.FirstOrDefault()?.Sha;
        sync.ToCommitSha = headSha;
        sync.Complete(rawCommits.Count, ticketCount, parsed.Count(p => p.result.IsBreaking), contributors);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation(
            "Sync {SyncId} for {RepoName} succeeded: {CommitCount} commits, {TicketCount} tickets, {ElapsedMs}ms",
            sync.Id, repo.Name, rawCommits.Count, ticketCount, sw.ElapsedMilliseconds);

        await InvalidateSnapshotCacheAsync(repo.Id, ct);
        _events.CloseChannel(sync.Id);
    }

    private async Task StepAsync(RepositorySync sync, string step, Stopwatch sw, CancellationToken ct)
    {
        sync.SetStep(step);
        await _db.SaveChangesAsync(ct);

        var evt = new SyncEvent(step, sync.RepositoryId, sync.Repository.Name, sync.Status, step,
            sw.ElapsedMilliseconds, new SyncCounts(sync.CommitCount, sync.TicketCount, sync.ContributorCount, sync.BreakingChangeCount));
        await _events.PublishAsync(sync.Id, evt, ct);
    }

    private ProviderConnection BuildProviderConnection(Repository repo)
    {
        var pat = _protector.Unprotect(repo.GitProviderConnection.EncryptedPat);
        return new ProviderConnection(repo.GitProviderConnection.OrganizationUrl, pat, repo.GitProviderConnection.ProviderType);
    }

    private async Task UpsertCommitsAsync(
        Guid repositoryId,
        List<(CommitInfo commit, ParsedCommit result)> items,
        CancellationToken ct)
    {
        var existing = await _db.Commits
            .Where(c => c.RepositoryId == repositoryId)
            .ToDictionaryAsync(c => c.Sha, ct);

        foreach (var (c, p) in items)
        {
            if (existing.TryGetValue(c.Sha, out var row))
            {
                row.Message        = c.Message;
                row.AuthorName     = c.AuthorName;
                row.AuthorEmail    = c.AuthorEmail;
                row.Type           = p.Type;
                row.Scope          = p.Scope;
                row.Description    = p.Description;
                row.IsBreaking     = p.IsBreaking;
                row.IsConventional = p.IsConventional;
                row.JiraTicketId   = p.JiraTicketId;
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
                    Type           = p.Type,
                    Scope          = p.Scope,
                    Description    = p.Description,
                    IsBreaking     = p.IsBreaking,
                    IsConventional = p.IsConventional,
                    JiraTicketId   = p.JiraTicketId
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<int> AggregateTicketsAsync(Guid repositoryId, string fromTag, CancellationToken ct)
    {
        var commits = await _db.Commits
            .Where(c => c.RepositoryId == repositoryId && c.JiraTicketId != null)
            .ToListAsync(ct);

        var old = await _db.Tickets
            .Where(t => t.RepositoryId == repositoryId && t.FromTag == fromTag)
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
                ToTag            = "HEAD",
                PrimaryType      = ComputePrimaryType(g),
                IsBreaking       = g.Any(c => c.IsBreaking),
                CommitCount      = g.Count,
                ContributorCount = g.Select(c => c.AuthorEmail).Distinct().Count(),
                FirstCommittedAt = g.Min(c => c.CommittedAt),
                LastCommittedAt  = g.Max(c => c.CommittedAt)
            });
        }

        await _db.SaveChangesAsync(ct);
        return commits.GroupBy(c => c.JiraTicketId!).Count();
    }

    private static List<ContributorSnapshot> BuildContributors(List<CommitInfo> commits)
    {
        var map = new Dictionary<string, (string Name, string Email, int Count)>(StringComparer.OrdinalIgnoreCase);

        foreach (var c in commits)
        {
            var key = !string.IsNullOrWhiteSpace(c.AuthorEmail)
                ? c.AuthorEmail.ToLowerInvariant()
                : c.AuthorName.ToLowerInvariant();

            if (map.TryGetValue(key, out var existing))
                map[key] = (existing.Name, existing.Email, existing.Count + 1);
            else
                map[key] = (c.AuthorName, c.AuthorEmail ?? string.Empty, 1);
        }

        return map.Values.Select(v => new ContributorSnapshot(v.Name, v.Email, v.Count)).ToList();
    }

    private static string? ComputePrimaryType(IList<Commit> commits)
    {
        var breaking = commits.FirstOrDefault(c => c.IsBreaking);
        if (breaking != null) return breaking.Type;
        foreach (var priority in new[] { "feat", "fix" })
            if (commits.Any(c => c.Type == priority)) return priority;
        var nonChore = commits.FirstOrDefault(c => c.Type != null && c.Type != "chore");
        if (nonChore != null) return nonChore.Type;
        return commits.Any(c => c.Type == "chore") ? "chore" : null;
    }

    // ── Read operations ─────────────────────────────────────────────────────

    public async Task<RepositorySyncDto?> GetLatestAsync(Guid repositoryId, CancellationToken ct = default)
    {
        var repo = await _db.Repositories.FindAsync([repositoryId], ct);
        if (repo == null) return null;

        var sync = await _db.RepositorySyncs
            .Where(s => s.RepositoryId == repositoryId && s.FromTag == repo.LatestTag)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

        return sync == null ? null : ToDto(sync);
    }

    public async Task<RepositorySyncDto?> GetByIdAsync(Guid syncId, CancellationToken ct = default)
    {
        var sync = await _db.RepositorySyncs.FindAsync([syncId], ct);
        return sync == null ? null : ToDto(sync);
    }

    private async Task InvalidateSnapshotCacheAsync(Guid repositoryId, CancellationToken ct)
    {
        var projectIds = await _db.ProjectRepositories
            .Where(pr => pr.RepositoryId == repositoryId)
            .Select(pr => pr.ProjectId)
            .ToListAsync(ct);
        foreach (var pid in projectIds)
            _snapshot.InvalidateCache(pid);
    }

    // ── DTO mapping ─────────────────────────────────────────────────────────

    internal static RepositorySyncDto ToDto(RepositorySync s)
    {
        List<ContributorSnapshotDto> contributors;
        try
        {
            contributors = JsonSerializer.Deserialize<List<ContributorSnapshot>>(s.ContributorsJson)
                ?.Select(c => new ContributorSnapshotDto(c.Name, c.Email, c.Commits))
                .ToList() ?? [];
        }
        catch
        {
            contributors = [];
        }

        return new RepositorySyncDto(
            s.Id, s.RepositoryId, s.ProjectSyncId, s.FromTag, s.ToCommitSha,
            s.Status, s.SkipReason, s.CurrentStep, s.StartedAt, s.CompletedAt,
            s.CommitCount, s.TicketCount, s.ContributorCount, s.BreakingChangeCount,
            contributors, s.ErrorMessage);
    }
}
