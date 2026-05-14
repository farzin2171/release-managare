using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Jira;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Jira;

public class JiraConnectionService : IJiraConnectionService
{
    private readonly AppDbContext _db;
    private readonly IJiraService _jiraService;
    private readonly IDataProtector _protector;
    private readonly ILogger<JiraConnectionService> _logger;

    public JiraConnectionService(AppDbContext db, IJiraService jiraService, IDataProtectionProvider dataProtection, ILogger<JiraConnectionService> logger)
    {
        _db = db;
        _jiraService = jiraService;
        _protector = dataProtection.CreateProtector("JiraConnection.ApiToken");
        _logger = logger;
    }

    public async Task<JiraConnectionDetailDto?> GetAsync(CancellationToken ct = default)
    {
        var entity = await _db.JiraConnections.FirstOrDefaultAsync(c => c.IsActive, ct);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<JiraConnectionDetailDto> UpsertAsync(UpsertJiraConnectionDto dto, CancellationToken ct = default)
    {
        var entity = await _db.JiraConnections.FirstOrDefaultAsync(c => c.IsActive, ct);
        if (entity is null)
        {
            entity = new JiraConnection { Id = Guid.NewGuid(), IsActive = true };
            _db.JiraConnections.Add(entity);
        }
        entity.BaseUrl = dto.BaseUrl.TrimEnd('/');
        entity.Username = dto.Email;
        entity.EncryptedApiToken = _protector.Protect(dto.ApiToken);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Jira connection {ConnectionId} upserted for {BaseUrl}", entity.Id, entity.BaseUrl);
        return ToDto(entity);
    }

    public async Task<TestJiraConnectionResultDto> TestAsync(UpsertJiraConnectionDto dto, CancellationToken ct = default)
    {
        var conn = new JiraConnectionDto(dto.BaseUrl.TrimEnd('/'), dto.Email, dto.ApiToken);
        var success = await _jiraService.TestConnectionAsync(conn, ct);
        return new TestJiraConnectionResultDto(success, success ? "Connection successful." : "Connection failed.");
    }

    public async Task<IReadOnlyList<JiraProjectDto>> ListProjectsAsync(CancellationToken ct = default)
    {
        var entity = await _db.JiraConnections.FirstOrDefaultAsync(c => c.IsActive, ct)
            ?? throw new NotFoundException("JiraConnection", "active");
        return await _jiraService.ListProjectsAsync(entity.Id, ct);
    }

    private static JiraConnectionDetailDto ToDto(JiraConnection c) =>
        new(c.Id, c.BaseUrl, c.Username, c.IsActive, c.LastTestedAt, c.TestStatus);
}
