using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.DTOs;
using RepoManager.Application.Events;
using RepoManager.Application.Queues;
using RepoManager.Application.Services;
using RepoManager.Domain.Aggregates;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Sync;

public class ProjectSyncService : IProjectSyncService
{
    private readonly AppDbContext _db;
    private readonly ISyncJobQueue _queue;
    private readonly IProjectSyncEventPublisher _projectEvents;
    private readonly ISyncEventPublisher _repoEvents;
    private readonly IRepositorySyncService _repoSyncService;
    private readonly ProjectSyncCancellationRegistry _cancelRegistry;
    private readonly ILogger<ProjectSyncService> _logger;

    public ProjectSyncService(
        AppDbContext db, ISyncJobQueue queue,
        IProjectSyncEventPublisher projectEvents, ISyncEventPublisher repoEvents,
        IRepositorySyncService repoSyncService, ProjectSyncCancellationRegistry cancelRegistry,
        ILogger<ProjectSyncService> logger)
    {
        _db = db; _queue = queue; _projectEvents = projectEvents; _repoEvents = repoEvents;
        _repoSyncService = repoSyncService; _cancelRegistry = cancelRegistry; _logger = logger;
    }

    // ── T029: Enqueue path ──────────────────────────────────────────────────

    public async Task<ProjectSyncDto> EnqueueAsync(Guid projectId, Guid userId, CancellationToken ct = default)
    {
        _ = await _db.Projects.FindAsync([projectId], ct) ?? throw new NotFoundException("Project", projectId);

        var activeExists = await _db.ProjectSyncs.AnyAsync(
            p => p.ProjectId == projectId &&
                 (p.Status == ProjectSyncStatus.Pending || p.Status == ProjectSyncStatus.InProgress), ct);

        if (activeExists) throw new ConflictException("A project sync is already running for this project.");

        var repos = await GetProjectReposAsync(projectId, ct);
        var run = new ProjectSync
        {
            Id = Guid.NewGuid(), ProjectId = projectId,
            Status = ProjectSyncStatus.Pending, StartedAt = DateTimeOffset.UtcNow,
            TotalRepos = repos.Count, TriggeredByUserId = userId
        };

        try
        {
            _db.ProjectSyncs.Add(run);
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("A project sync is already running for this project.");
        }

        await _queue.EnqueueAsync(new SyncJob(Guid.Empty, Guid.Empty, run.Id), ct);
        return ToDto(run, null);
    }

    // ── Cancel path ─────────────────────────────────────────────────────────

    public async Task CancelActiveAsync(Guid projectId, CancellationToken ct = default)
    {
        _ = await _db.Projects.FindAsync([projectId], ct) ?? throw new NotFoundException("Project", projectId);

        var active = await _db.ProjectSyncs.FirstOrDefaultAsync(
            p => p.ProjectId == projectId &&
                 (p.Status == ProjectSyncStatus.Pending || p.Status == ProjectSyncStatus.InProgress), ct)
            ?? throw new NotFoundException("Active project sync for project", projectId);

        _cancelRegistry.TryCancel(active.Id);
    }

    // ── T029: Execute path (called by worker only) ──────────────────────────

    public async Task ExecuteAsync(Guid projectSyncId, CancellationToken workerCt = default)
    {
        var run = await _db.ProjectSyncs.FindAsync([projectSyncId], workerCt)
            ?? throw new NotFoundException("ProjectSync", projectSyncId);

        var cancelToken = _cancelRegistry.Register(projectSyncId);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancelToken, workerCt);

        _projectEvents.OpenStream(projectSyncId);
        run.Start();
        await _db.SaveChangesAsync(workerCt);

        var repos = await GetProjectReposAsync(run.ProjectId, workerCt);
        try
        {
            foreach (var repo in repos)
            {
                if (linked.Token.IsCancellationRequested) break;
                await ProcessRepoAsync(run, repo, workerCt);
            }
            await FinaliseRunAsync(run, linked.Token.IsCancellationRequested, workerCt);
        }
        catch (Exception ex)
        {
            await HandleExecutionFailureAsync(run, projectSyncId, ex, workerCt);
        }
        finally
        {
            _cancelRegistry.Unregister(projectSyncId);
        }
    }

    // ── Read operations ─────────────────────────────────────────────────────

    public async Task<ProjectSyncDto?> GetLatestAsync(Guid projectId, CancellationToken ct = default)
    {
        _ = await _db.Projects.FindAsync([projectId], ct) ?? throw new NotFoundException("Project", projectId);

        var run = await _db.ProjectSyncs
            .Include(p => p.RepositorySyncs)
            .Where(p => p.ProjectId == projectId)
            .OrderByDescending(p => p.StartedAt)
            .FirstOrDefaultAsync(ct);

        return run is null ? null : ToDto(run, run.RepositorySyncs);
    }

    public async Task<ProjectSyncDto?> GetActiveAsync(Guid projectId, CancellationToken ct = default)
    {
        _ = await _db.Projects.FindAsync([projectId], ct) ?? throw new NotFoundException("Project", projectId);

        var run = await _db.ProjectSyncs.FirstOrDefaultAsync(
            p => p.ProjectId == projectId &&
                 (p.Status == ProjectSyncStatus.Pending || p.Status == ProjectSyncStatus.InProgress), ct);

        return run is null ? null : ToDto(run, null);
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private async Task<List<Repository>> GetProjectReposAsync(Guid projectId, CancellationToken ct) =>
        await _db.ProjectRepositories
            .Where(pr => pr.ProjectId == projectId)
            .Select(pr => pr.Repository)
            .ToListAsync(ct);

    private async Task ProcessRepoAsync(ProjectSync run, Repository repo, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(repo.LatestTag))
        {
            await HandleNoTagAsync(run, repo, ct);
            return;
        }

        var repoSync = await CreateRepoSyncRowAsync(repo, run, ct);
        _repoEvents.CreateChannel(repoSync.Id);

        int done = run.SucceededCount + run.FailedCount + run.SkippedCount;
        await PublishAsync(run.Id, "repo_started",
            new { repoId = repo.Id, repoName = repo.Name, syncId = repoSync.Id, totalRepos = run.TotalRepos, completedCount = done }, ct);

        var relayTask = RelayStepEventsAsync(run.Id, repoSync.Id, ct);
        await _repoSyncService.ExecuteAsync(repoSync.Id, ct);
        await relayTask;

        await _db.Entry(repoSync).ReloadAsync(ct);
        run.RecordChildResult(repoSync.Status);
        await _db.SaveChangesAsync(ct);

        await PublishAsync(run.Id, "repo_completed",
            new { repoId = repo.Id, syncId = repoSync.Id, status = repoSync.Status.ToString(), commitCount = repoSync.CommitCount, ticketCount = repoSync.TicketCount, breakingChangeCount = repoSync.BreakingChangeCount, contributorCount = repoSync.ContributorCount, errorMessage = repoSync.ErrorMessage }, ct);
    }

    private async Task<RepositorySync> CreateRepoSyncRowAsync(Repository repo, ProjectSync run, CancellationToken ct)
    {
        var row = new RepositorySync
        {
            Id = Guid.NewGuid(), RepositoryId = repo.Id, ProjectSyncId = run.Id,
            FromTag = repo.LatestTag!, Status = SyncStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow, TriggeredByUserId = run.TriggeredByUserId
        };
        _db.RepositorySyncs.Add(row);
        await _db.SaveChangesAsync(ct);
        return row;
    }

    private async Task HandleNoTagAsync(ProjectSync run, Repository repo, CancellationToken ct)
    {
        var skipped = new RepositorySync
        {
            Id = Guid.NewGuid(), RepositoryId = repo.Id, ProjectSyncId = run.Id,
            FromTag = string.Empty, Status = SyncStatus.Pending,
            StartedAt = DateTimeOffset.UtcNow, TriggeredByUserId = run.TriggeredByUserId
        };
        skipped.Skip("NoPinnedTag");
        _db.RepositorySyncs.Add(skipped);
        run.RecordChildResult(SyncStatus.Skipped);
        await _db.SaveChangesAsync(ct);
        await PublishAsync(run.Id, "repo_completed",
            new { repoId = repo.Id, syncId = skipped.Id, status = "Skipped", commitCount = 0, ticketCount = 0, breakingChangeCount = 0, contributorCount = 0, errorMessage = (string?)null }, ct);
    }

    private async Task RelayStepEventsAsync(Guid projectSyncId, Guid repoSyncId, CancellationToken ct)
    {
        await foreach (var evt in _repoEvents.SubscribeAsync(repoSyncId, ct))
        {
            if (evt.CurrentStep is null) continue;
            await PublishAsync(projectSyncId, "step_changed",
                new { repoId = evt.RepoId, syncId = repoSyncId, currentStep = evt.CurrentStep, elapsedMs = evt.ElapsedMs }, ct);
        }
    }

    private async Task FinaliseRunAsync(ProjectSync run, bool wasCancelled, CancellationToken ct)
    {
        if (wasCancelled) run.Cancel(); else run.Complete();
        await _db.SaveChangesAsync(ct);

        await PublishAsync(run.Id, "project_complete",
            new { projectSyncId = run.Id, status = run.Status.ToString(), succeededCount = run.SucceededCount, failedCount = run.FailedCount, skippedCount = run.SkippedCount, completedAt = run.CompletedAt }, ct);
        _projectEvents.CloseStream(run.Id);

        _logger.LogInformation("ProjectSync {SyncId} finished: {Status}, {Succeeded} succeeded, {Failed} failed, {Skipped} skipped",
            run.Id, run.Status, run.SucceededCount, run.FailedCount, run.SkippedCount);
    }

    private async Task HandleExecutionFailureAsync(ProjectSync run, Guid projectSyncId, Exception ex, CancellationToken ct)
    {
        _logger.LogError(ex, "ProjectSync {SyncId} failed unexpectedly", projectSyncId);
        if (run.Status == ProjectSyncStatus.InProgress)
        {
            run.Cancel();
            await _db.SaveChangesAsync(ct);
        }
        _projectEvents.CloseStream(projectSyncId);
    }

    private async ValueTask PublishAsync(Guid projectSyncId, string @event, object payload, CancellationToken ct) =>
        await _projectEvents.PublishAsync(projectSyncId, @event, JsonSerializer.Serialize(payload), ct);

    private static ProjectSyncDto ToDto(ProjectSync s, IEnumerable<RepositorySync>? childSyncs) =>
        new(s.Id, s.ProjectId, s.Status, s.StartedAt, s.CompletedAt,
            s.TotalRepos, s.SucceededCount, s.FailedCount, s.SkippedCount,
            s.TriggeredByUserId,
            childSyncs?.Select(RepositorySyncService.ToDto).ToList());
}
