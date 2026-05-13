using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Repositories;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Repositories;

public class RepositoryService : IRepositoryService
{
    private readonly AppDbContext _db;

    public RepositoryService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<RepositoryDto>> ListAsync(ListRepositoriesQuery query, CancellationToken ct = default)
    {
        var q = _db.Repositories.AsQueryable();

        if (query.ConnectionId.HasValue)
            q = q.Where(r => r.GitProviderConnectionId == query.ConnectionId.Value);

        if (query.IsTracked.HasValue)
            q = q.Where(r => r.IsTracked == query.IsTracked.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(r => r.Name.Contains(query.Search));

        var repos = await q.OrderBy(r => r.Name).ToListAsync(ct);
        return repos.Select(ToDto).ToList();
    }

    public async Task<RepositoryDto> SetTrackedAsync(Guid id, SetTrackedDto dto, CancellationToken ct = default)
    {
        var repo = await _db.Repositories.FindAsync([id], ct)
            ?? throw new NotFoundException("Repository", id);

        repo.IsTracked = dto.IsTracked;
        await _db.SaveChangesAsync(ct);
        return ToDto(repo);
    }

    private static RepositoryDto ToDto(Repository r) =>
        new(r.Id, r.GitProviderConnectionId, r.ExternalId, r.Name,
            r.DefaultBranch, r.WebUrl, r.AzureProjectName, r.IsTracked, r.LastSyncedAt);
}
