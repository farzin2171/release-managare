using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.GitProviders;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.GitProviders;

public class GitProviderConnectionService : IGitProviderConnectionService
{
    private readonly AppDbContext _db;
    private readonly IGitProviderFactory _providerFactory;
    private readonly IDataProtector _protector;
    private readonly ILogger<GitProviderConnectionService> _logger;

    public GitProviderConnectionService(
        AppDbContext db,
        IGitProviderFactory providerFactory,
        IDataProtectionProvider dataProtection,
        ILogger<GitProviderConnectionService> logger)
    {
        _db = db;
        _providerFactory = providerFactory;
        _protector = dataProtection.CreateProtector("GitProviderConnection.Pat");
        _logger = logger;
    }

    public async Task<GitProviderConnectionDto> CreateAsync(CreateGitConnectionDto dto, CancellationToken ct = default)
    {
        var connection = new GitProviderConnection
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            ProviderType = dto.ProviderType,
            OrganizationUrl = dto.OrganizationUrl.TrimEnd('/'),
            EncryptedPat = _protector.Protect(dto.Pat),
            IsActive = true
        };
        _db.GitProviderConnections.Add(connection);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Git provider connection {ConnectionId} created for {OrganizationUrl}", connection.Id, connection.OrganizationUrl);
        return ToDto(connection);
    }

    public async Task<IReadOnlyList<GitProviderConnectionDto>> ListAsync(CancellationToken ct = default)
    {
        var connections = await _db.GitProviderConnections
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        return connections.Select(ToDto).ToList();
    }

    public async Task<GitProviderConnectionDto> UpdateAsync(Guid id, UpdateGitConnectionDto dto, CancellationToken ct = default)
    {
        var connection = await _db.GitProviderConnections.FindAsync([id], ct)
            ?? throw new NotFoundException("GitProviderConnection", id);

        if (dto.Name is not null) connection.Name = dto.Name;
        if (dto.OrganizationUrl is not null) connection.OrganizationUrl = dto.OrganizationUrl.TrimEnd('/');
        if (dto.Pat is not null) connection.EncryptedPat = _protector.Protect(dto.Pat);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Git provider connection {ConnectionId} updated", id);
        return ToDto(connection);
    }

    public async Task<TestConnectionResultDto> TestAsync(TestGitConnectionDto dto, CancellationToken ct = default)
    {
        var provider = _providerFactory.GetProvider(dto.ProviderType);
        var conn = new ProviderConnection(dto.OrganizationUrl.TrimEnd('/'), dto.Pat, dto.ProviderType);
        var success = await provider.TestConnectionAsync(conn, ct);
        return new TestConnectionResultDto(success, success ? "Connection successful." : "Connection failed.");
    }

    public async Task<TestConnectionResultDto> TestByIdAsync(Guid id, CancellationToken ct = default)
    {
        var connection = await _db.GitProviderConnections.FindAsync([id], ct)
            ?? throw new NotFoundException("GitProviderConnection", id);

        var pat = _protector.Unprotect(connection.EncryptedPat);
        var provider = _providerFactory.GetProvider(connection.ProviderType);
        var conn = new ProviderConnection(connection.OrganizationUrl, pat, connection.ProviderType);
        var success = await provider.TestConnectionAsync(conn, ct);

        connection.LastTestStatus = success ? "Success" : "Failed";
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Git provider connection {ConnectionId} test result: {Status}", id, connection.LastTestStatus);
        return new TestConnectionResultDto(success, success ? "Connection successful." : "Connection failed.");
    }

    public async Task SyncAsync(Guid id, CancellationToken ct = default)
    {
        var connection = await _db.GitProviderConnections.FindAsync([id], ct)
            ?? throw new NotFoundException("GitProviderConnection", id);

        var pat = _protector.Unprotect(connection.EncryptedPat);
        var provider = _providerFactory.GetProvider(connection.ProviderType);
        var conn = new ProviderConnection(connection.OrganizationUrl, pat, connection.ProviderType);

        var remoteRepos = await provider.ListRepositoriesAsync(conn, ct);

        var existing = await _db.Repositories
            .Where(r => r.GitProviderConnectionId == id)
            .ToListAsync(ct);

        var existingByExternalId = existing.ToDictionary(r => r.ExternalId);

        foreach (var remote in remoteRepos)
        {
            if (existingByExternalId.TryGetValue(remote.ExternalId, out var repo))
            {
                repo.Name = remote.Name;
                repo.DefaultBranch = remote.DefaultBranch;
                repo.WebUrl = remote.WebUrl;
                repo.AzureProjectName = remote.AzureProjectName;
            }
            else
            {
                _db.Repositories.Add(new Repository
                {
                    Id = Guid.NewGuid(),
                    GitProviderConnectionId = id,
                    ExternalId = remote.ExternalId,
                    Name = remote.Name,
                    DefaultBranch = remote.DefaultBranch,
                    WebUrl = remote.WebUrl,
                    AzureProjectName = remote.AzureProjectName,
                    IsTracked = false
                });
            }
        }

        connection.LastSyncedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Git provider connection {ConnectionId} synced {Count} repositories", id, remoteRepos.Count());
    }

    private static GitProviderConnectionDto ToDto(GitProviderConnection c) =>
        new(c.Id, c.Name, c.ProviderType, c.OrganizationUrl, c.IsActive, c.LastSyncedAt, c.LastTestStatus);
}
