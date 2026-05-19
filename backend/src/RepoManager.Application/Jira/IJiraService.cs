using RepoManager.Application.Jira.Dtos;
using RepoManager.Domain.Enums;

namespace RepoManager.Application.Jira;

public interface IJiraService
{
    Task<bool> TestConnectionAsync(JiraConnectionDto conn, CancellationToken ct = default);
    Task<IReadOnlyList<JiraProjectDto>> ListProjectsAsync(Guid connectionId, CancellationToken ct = default);
    Task<JiraReleaseDto> SyncFixVersionAsync(Guid connectionId, string projectKey, string versionName, bool createIfMissing, CancellationToken ct = default);
    Task AddTicketToFixVersionAsync(Guid connectionId, string ticketKey, string versionId, CancellationToken ct = default);

    Task<IReadOnlyList<JiraIssueSummary>> GetTicketsInFixVersionAsync(
        IEnumerable<string> jiraProjectKeys,
        string fixVersionName,
        CancellationToken ct = default);

    Task AddTicketToFixVersionAsync(
        string ticketKey,
        string fixVersionName,
        CancellationToken ct = default);

    Task<string> CreateFixVersionAsync(
        string jiraProjectKey,
        string fixVersionName,
        CancellationToken ct = default);
}

public record JiraConnectionDto(string BaseUrl, string Username, string DecryptedApiToken);
public record JiraProjectDto(string Key, string Name, string ProjectType);
public record JiraReleaseDto(string JiraVersionId, string Name, bool IsReleased, DateOnly? ReleaseDate, IReadOnlyList<JiraTicketDto> Tickets);
public record JiraTicketDto(string Key, string Summary, string Status, JiraStatusCategory StatusCategory, string IssueType, string? AssigneeName, string? AssigneeEmail, string? Priority, string? ParentKey);
