using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.GitProviders;
using RepoManager.Domain.ValueObjects;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.GitProviders;

public class GitProviderService : IGitProviderService
{
    private readonly AppDbContext _db;
    private readonly IGitProviderFactory _factory;
    private readonly IDataProtector _protector;

    public GitProviderService(
        AppDbContext db,
        IGitProviderFactory factory,
        IDataProtectionProvider dataProtection)
    {
        _db = db;
        _factory = factory;
        _protector = dataProtection.CreateProtector("GitProviderConnection.Pat");
    }

    public async Task<IReadOnlyList<RepositoryTag>> ListTagsAsync(Guid repositoryId, CancellationToken ct = default)
    {
        var repo = await _db.Repositories
            .Include(r => r.GitProviderConnection)
            .FirstOrDefaultAsync(r => r.Id == repositoryId, ct)
            ?? throw new NotFoundException("Repository", repositoryId);

        var connection = repo.GitProviderConnection;
        var pat = _protector.Unprotect(connection.EncryptedPat);
        var conn = new ProviderConnection(connection.OrganizationUrl, pat, connection.ProviderType);

        var provider = _factory.GetProvider(connection.ProviderType);
        var tags = await provider.ListTagsAsync(conn, repo.ExternalId, ct);

        return tags
            .Select(t => new RepositoryTag(t.Name, t.CommitSha, t.CommitDate, t.AuthorName))
            .ToList();
    }
}
