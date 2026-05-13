namespace RepoManager.Application.Jira;

public interface IJiraConnectionService
{
    Task<JiraConnectionDetailDto?> GetAsync(CancellationToken ct = default);
    Task<JiraConnectionDetailDto> UpsertAsync(UpsertJiraConnectionDto dto, CancellationToken ct = default);
    Task<TestJiraConnectionResultDto> TestAsync(UpsertJiraConnectionDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<JiraProjectDto>> ListProjectsAsync(CancellationToken ct = default);
}

public record UpsertJiraConnectionDto(string BaseUrl, string Email, string ApiToken);

public record JiraConnectionDetailDto(
    Guid Id,
    string BaseUrl,
    string Email,
    bool IsActive,
    DateTimeOffset? LastTestedAt,
    string? TestStatus);

public record TestJiraConnectionResultDto(bool Success, string Message);
