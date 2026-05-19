using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Maintenance;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Maintenance;

public class DatabaseMaintenanceService : IDatabaseMaintenanceService
{
    private readonly AppDbContext _context;

    public DatabaseMaintenanceService(AppDbContext context) => _context = context;

    public async Task ResetDatabaseAsync(CancellationToken ct = default)
    {
        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        await _context.RepoJiraComparisonSnapshots.ExecuteDeleteAsync(ct);
        await _context.ReleaseReconciliations.ExecuteDeleteAsync(ct);
        await _context.JiraTickets.ExecuteDeleteAsync(ct);
        await _context.JiraReleases.ExecuteDeleteAsync(ct);
        await _context.ReleaseRepositoryTags.ExecuteDeleteAsync(ct);
        await _context.RepositorySyncs.ExecuteDeleteAsync(ct);
        await _context.ProjectSyncs.ExecuteDeleteAsync(ct);
        await _context.Releases.ExecuteDeleteAsync(ct);
        await _context.Tickets.ExecuteDeleteAsync(ct);
        await _context.Commits.ExecuteDeleteAsync(ct);
        await _context.ProjectRepositories.ExecuteDeleteAsync(ct);
        await _context.Projects.ExecuteDeleteAsync(ct);
        await _context.Repositories.ExecuteDeleteAsync(ct);

        await tx.CommitAsync(ct);
    }
}
