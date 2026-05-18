using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Jira;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.BackgroundServices;

public class JiraCoverageRefreshService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<JiraCoverageRefreshService> _logger;

    public JiraCoverageRefreshService(
        IServiceProvider services,
        ILogger<JiraCoverageRefreshService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));

        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct))
        {
            await RefreshStaleSnapshotsAsync(ct);
        }
    }

    private async Task RefreshStaleSnapshotsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var comparisonService = scope.ServiceProvider.GetRequiredService<IRepoJiraComparisonService>();

        var now = DateTime.UtcNow;
        var viewedSince = now.AddHours(-24);
        var staleBefore = now.AddMinutes(-5);

        var repoIds = await db.Repositories
            .Where(r => r.LastViewedAt != null
                && r.LastViewedAt > viewedSince
                && r.JiraComparisonSnapshots.Any(s => s.LastSyncedAt < staleBefore))
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (repoIds.Count == 0)
            return;

        _logger.LogInformation(
            "jira_coverage.background_refresh starting repoCount={RepoCount}", repoIds.Count);

        var refreshed = 0;
        var failed = 0;

        foreach (var repoId in repoIds)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await comparisonService.GetForRepoAsync(repoId, forceRefresh: true, ct);
                refreshed++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogWarning(ex,
                    "jira_coverage.background_refresh failed repoId={RepositoryId}", repoId);
            }
        }

        _logger.LogInformation(
            "jira_coverage.background_refresh completed refreshed={Refreshed} failed={Failed}",
            refreshed, failed);
    }
}
