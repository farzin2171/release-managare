namespace RepoManager.Application.Releases;

public interface IReleaseService
{
    Task<ReleaseDto> CreateAsync(Guid projectId, CreateReleaseDto dto, Guid createdByUserId, CancellationToken ct = default);
    Task<ReleaseDto> GetAsync(Guid id, CancellationToken ct = default);
    Task<ReleaseDto> UpdateNotesAsync(Guid id, UpdateNotesDto dto, CancellationToken ct = default);
    Task<ReleaseDto> PublishAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ReleaseDto>> ListByProjectAsync(Guid projectId, CancellationToken ct = default);
}

public record CreateReleaseDto(
    string Version,
    Guid TemplateId,
    IReadOnlyList<RepositoryTagRangeDto> RepositoryTags);

public record RepositoryTagRangeDto(
    Guid RepositoryId,
    string FromTag,
    string ToTag);

public record UpdateNotesDto(string EditedNotesMarkdown);

public record ReleaseRepositoryTagDto(
    Guid RepositoryId,
    string RepositoryName,
    string FromTag,
    string ToTag,
    int CommitCount);

public record ReleaseDto(
    Guid Id,
    Guid ProjectId,
    string Version,
    string Status,
    string GeneratedNotesMarkdown,
    string? EditedNotesMarkdown,
    string? ConfluencePageId,
    string? ConfluencePageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    string SuggestedNextVersion,
    IReadOnlyList<ReleaseRepositoryTagDto> RepositoryTags);
