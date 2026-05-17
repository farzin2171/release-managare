using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RepoManager.Application.DTOs;
using RepoManager.Application.Services;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Sync;

public class ProjectSyncSnapshotService : IProjectSyncSnapshotService
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;

    private static string CacheKey(Guid projectId) => $"snapshot:{projectId}";

    public ProjectSyncSnapshotService(AppDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IEnumerable<RepoSyncSnapshotItemDto>> GetSnapshotAsync(Guid projectId, CancellationToken ct = default)
    {
        var key = CacheKey(projectId);
        if (_cache.TryGetValue(key, out IEnumerable<RepoSyncSnapshotItemDto>? cached) && cached != null)
            return cached;

        var result = await FetchAsync(projectId, ct);
        _cache.Set(key, result, new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromSeconds(5) });
        return result;
    }

    public void InvalidateCache(Guid projectId) => _cache.Remove(CacheKey(projectId));

    private async Task<IEnumerable<RepoSyncSnapshotItemDto>> FetchAsync(Guid projectId, CancellationToken ct)
    {
        var projectRepos = await _db.ProjectRepositories
            .Include(pr => pr.Repository)
            .Where(pr => pr.ProjectId == projectId)
            .ToListAsync(ct);

        if (projectRepos.Count == 0)
            return [];

        var repoIds = projectRepos.Select(pr => pr.RepositoryId).ToList();

        var allSucceeded = await _db.RepositorySyncs
            .Where(s => repoIds.Contains(s.RepositoryId) && s.Status == SyncStatus.Succeeded)
            .ToListAsync(ct);

        var inProgressSyncs = await _db.RepositorySyncs
            .Where(s => repoIds.Contains(s.RepositoryId) &&
                        (s.Status == SyncStatus.Pending || s.Status == SyncStatus.InProgress))
            .ToListAsync(ct);

        var latestSuccessMap = allSucceeded
            .GroupBy(s => s.RepositoryId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.StartedAt).First());

        var inProgressMap = inProgressSyncs
            .GroupBy(s => s.RepositoryId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.StartedAt).First());

        return projectRepos.Select(pr =>
        {
            var repo = pr.Repository;
            latestSuccessMap.TryGetValue(repo.Id, out var latestSync);
            inProgressMap.TryGetValue(repo.Id, out var inProgress);

            var validSync = latestSync != null && latestSync.FromTag == (repo.LatestTag ?? string.Empty)
                ? latestSync
                : null;

            return new RepoSyncSnapshotItemDto(
                repo.Id,
                repo.Name,
                repo.LatestTag,
                validSync != null ? RepositorySyncService.ToDto(validSync) : null,
                inProgress?.CurrentStep);
        }).ToList();
    }
}
