namespace RepoManager.Application.Confluence;

public interface IConfluenceConnectionService
{
    Task<ConfluenceConnectionDetailDto?> GetAsync(CancellationToken ct = default);
    Task<ConfluenceConnectionDetailDto> UpsertAsync(UpsertConfluenceConnectionDto dto, CancellationToken ct = default);
    Task<TestConfluenceConnectionResultDto> TestAsync(UpsertConfluenceConnectionDto dto, CancellationToken ct = default);
}

public record UpsertConfluenceConnectionDto(string BaseUrl, string Email, string ApiToken);
public record ConfluenceConnectionDetailDto(Guid Id, string BaseUrl, string Email, bool IsActive, DateTimeOffset? LastTestedAt, string? LastTestStatus);
public record TestConfluenceConnectionResultDto(bool Success, string Message);
