using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Confluence;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Confluence;

public class ConfluenceConnectionService : IConfluenceConnectionService
{
    private readonly AppDbContext _db;
    private readonly IConfluencePublisher _publisher;
    private readonly IDataProtector _protector;
    private readonly ILogger<ConfluenceConnectionService> _logger;

    public ConfluenceConnectionService(AppDbContext db, IConfluencePublisher publisher, IDataProtectionProvider dataProtection, ILogger<ConfluenceConnectionService> logger)
    {
        _db = db;
        _publisher = publisher;
        _protector = dataProtection.CreateProtector("ConfluenceConnection.ApiToken");
        _logger = logger;
    }

    public async Task<ConfluenceConnectionDetailDto?> GetAsync(CancellationToken ct = default)
    {
        var entity = await _db.ConfluenceConnections.FirstOrDefaultAsync(c => c.IsActive, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<ConfluenceConnectionDetailDto> UpsertAsync(UpsertConfluenceConnectionDto dto, CancellationToken ct = default)
    {
        var entity = await _db.ConfluenceConnections.FirstOrDefaultAsync(c => c.IsActive, ct);
        if (entity is null)
        {
            entity = new ConfluenceConnection { Id = Guid.NewGuid(), IsActive = true };
            _db.ConfluenceConnections.Add(entity);
        }
        entity.BaseUrl = dto.BaseUrl.TrimEnd('/');
        entity.Username = dto.Email;
        entity.EncryptedApiToken = _protector.Protect(dto.ApiToken);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Confluence connection {ConnectionId} upserted for {BaseUrl}", entity.Id, entity.BaseUrl);
        return ToDto(entity);
    }

    public async Task<TestConfluenceConnectionResultDto> TestAsync(UpsertConfluenceConnectionDto dto, CancellationToken ct = default)
    {
        var conn = new ConfluenceConnectionDto(dto.BaseUrl.TrimEnd('/'), dto.Email, dto.ApiToken);
        var success = await _publisher.TestConnectionAsync(conn, ct);
        return new TestConfluenceConnectionResultDto(success, success ? "Connection successful." : "Connection failed.");
    }

    private static ConfluenceConnectionDetailDto ToDto(ConfluenceConnection c) =>
        new(c.Id, c.BaseUrl, c.Username, c.IsActive, c.LastTestedAt, c.LastTestStatus);
}
