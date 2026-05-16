using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Queues;
using RepoManager.Application.Services;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Sync;

public class SyncBackgroundService : BackgroundService
{
    private readonly ISyncJobQueue _queue;
    private readonly IServiceProvider _services;
    private readonly ILogger<SyncBackgroundService> _logger;

    public SyncBackgroundService(
        ISyncJobQueue queue,
        IServiceProvider services,
        ILogger<SyncBackgroundService> logger)
    {
        _queue = queue;
        _services = services;
        _logger = logger;
    }

    public override async Task StartAsync(CancellationToken ct)
    {
        await RecoverStaleSync(ct);
        await base.StartAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Sync background service started");

        while (!ct.IsCancellationRequested)
        {
            SyncJob job;
            try
            {
                job = await _queue.DequeueAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await DispatchJobAsync(job, ct);
        }
    }

    private async Task DispatchJobAsync(SyncJob job, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IRepositorySyncService>();

        try
        {
            await syncService.ExecuteAsync(job.RepositorySyncId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing sync job {SyncId}", job.RepositorySyncId);
        }
    }

    private async Task RecoverStaleSync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-30);

        var staleRepoSyncs = await db.RepositorySyncs
            .Where(s => s.Status == SyncStatus.InProgress && s.StartedAt < cutoff)
            .ToListAsync(ct);

        foreach (var sync in staleRepoSyncs)
        {
            sync.Fail("Stale — worker restarted");
            _logger.LogWarning("Marked stale RepositorySync {SyncId} as Failed", sync.Id);
        }

        var staleProjectSyncs = await db.ProjectSyncs
            .Where(s => s.Status == ProjectSyncStatus.InProgress && s.StartedAt < cutoff)
            .ToListAsync(ct);

        foreach (var sync in staleProjectSyncs)
        {
            sync.Cancel();
            _logger.LogWarning("Marked stale ProjectSync {SyncId} as Cancelled", sync.Id);
        }

        if (staleRepoSyncs.Count > 0 || staleProjectSyncs.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
