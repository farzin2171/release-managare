using RepoManager.Application.DTOs;

namespace RepoManager.Application.Services;

public interface IRepositorySyncService
{
    Task<RepositorySyncDto> EnqueueAsync(Guid repositoryId, Guid userId, CancellationToken ct = default);
    Task ExecuteAsync(Guid repositorySyncId, CancellationToken ct = default);
    Task<RepositorySyncDto?> GetLatestAsync(Guid repositoryId, CancellationToken ct = default);
    Task<RepositorySyncDto?> GetByIdAsync(Guid syncId, CancellationToken ct = default);
}
