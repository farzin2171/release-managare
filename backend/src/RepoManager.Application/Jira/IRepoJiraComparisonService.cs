using RepoManager.Application.Jira.Dtos;

namespace RepoManager.Application.Jira;

public interface IRepoJiraComparisonService
{
    Task<RepoJiraComparisonDto> GetForRepoAsync(
        Guid repositoryId,
        bool forceRefresh,
        CancellationToken ct = default);

    Task<ProjectJiraCoverageDto> GetForProjectAsync(
        Guid projectId,
        bool forceRefresh,
        CancellationToken ct = default);

    Task<AddToFixVersionResultDto> AddTicketToFixVersionAsync(
        Guid repositoryId,
        string ticketKey,
        CancellationToken ct = default);
}
