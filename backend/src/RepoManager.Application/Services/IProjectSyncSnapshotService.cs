using RepoManager.Application.DTOs;

namespace RepoManager.Application.Services;

public interface IProjectSyncSnapshotService
{
    Task<IEnumerable<RepoSyncSnapshotItemDto>> GetSnapshotAsync(Guid projectId, CancellationToken ct = default);
    void InvalidateCache(Guid projectId);
}
