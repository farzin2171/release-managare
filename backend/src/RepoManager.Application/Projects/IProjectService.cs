namespace RepoManager.Application.Projects;

public interface IProjectService
{
    Task<ProjectDto> CreateAsync(CreateProjectDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct = default);
    Task<ProjectDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<ProjectDto> AssignRepositoryAsync(Guid id, Guid repoId, AssignRepositoryDto dto, CancellationToken ct = default);
    Task<ProjectDto> RemoveRepositoryAsync(Guid id, Guid repoId, CancellationToken ct = default);
    Task<ProjectDto> ConfigureJiraAsync(Guid id, ConfigureJiraDto dto, CancellationToken ct = default);
}

public record CreateProjectDto(
    string Name,
    string? Description,
    string Color);

public record UpdateProjectDto(
    string? Name,
    string? Description,
    string? Color,
    Guid? ReleaseNoteTemplateId,
    string? ConfluenceSpaceKey,
    string? ConfluenceParentPageId);

public record AssignRepositoryDto(bool IsPrimary);

public record ConfigureJiraDto(
    Guid? JiraConnectionId,
    string[] JiraProjectKeys,
    string? FixVersionPattern,
    bool AutoCreateFixVersion,
    bool MatchSubtasksToParents);

public record ProjectRepositoryDto(
    Guid RepositoryId,
    string Name,
    string DefaultBranch,
    bool IsPrimary);

public record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    string Color,
    Guid? ReleaseNoteTemplateId,
    string? ConfluenceSpaceKey,
    string? ConfluenceParentPageId,
    Guid? JiraConnectionId,
    string[] JiraProjectKeys,
    string? FixVersionPattern,
    bool AutoCreateFixVersion,
    bool MatchSubtasksToParents,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ProjectRepositoryDto> Repositories);
