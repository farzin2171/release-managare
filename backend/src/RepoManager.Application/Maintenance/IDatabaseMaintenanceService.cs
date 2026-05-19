namespace RepoManager.Application.Maintenance;

public interface IDatabaseMaintenanceService
{
    Task ResetDatabaseAsync(CancellationToken ct = default);
}
