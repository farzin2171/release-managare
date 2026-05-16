using RepoManager.Application.DTOs;

namespace RepoManager.Application.Services;

public interface IProjectSyncService
{
    Task<ProjectSyncDto> EnqueueAsync(Guid projectId, Guid userId, CancellationToken ct = default);
    Task CancelActiveAsync(Guid projectId, CancellationToken ct = default);
    Task ExecuteAsync(Guid projectSyncId, CancellationToken ct = default);
    Task<ProjectSyncDto?> GetLatestAsync(Guid projectId, CancellationToken ct = default);
    Task<ProjectSyncDto?> GetActiveAsync(Guid projectId, CancellationToken ct = default);
}
