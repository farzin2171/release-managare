namespace RepoManager.Application.Releases;

public interface IReleaseCompositionService
{
    Task<ReleasePreviewDto> PreviewAsync(
        Guid projectId,
        IReadOnlyList<Guid> repositoryIds,
        CancellationToken ct = default);

    Task<ReleaseCompositionDto> CreateDraftAsync(
        Guid projectId,
        CreateReleaseRequest request,
        Guid createdByUserId,
        CancellationToken ct = default);

    Task<ReleaseCompositionDto> UpdateDraftAsync(
        Guid releaseId,
        UpdateReleaseRequest request,
        CancellationToken ct = default);

    Task<ReleaseCompositionDto> GetAsync(
        Guid releaseId,
        CancellationToken ct = default);

    // Returns null when lock is successfully acquired; returns lock-holder name when blocked.
    Task<string?> TryAcquireEditLockAsync(
        Guid releaseId,
        Guid userId,
        string userName,
        CancellationToken ct = default);

    Task ReleaseEditLockAsync(
        Guid releaseId,
        Guid userId,
        CancellationToken ct = default);

    Task DeleteDraftAsync(
        Guid releaseId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ReleaseSummaryDto>> ListByProjectAsync(
        Guid projectId,
        string? status,
        string? search,
        string? sort,
        string? order,
        CancellationToken ct = default);
}

public record CreateReleaseRequest(
    string Name,
    IReadOnlyList<ReleaseRepositorySelectionDto> Repositories);

public record UpdateReleaseRequest(
    IReadOnlyList<ReleaseRepositorySelectionDto> Repositories);

public record ReleaseRepositorySelectionDto(
    Guid RepositoryId,
    string NextVersion,
    string BumpType);

public record ReleasePreviewDto(
    IReadOnlyList<ReleasePreviewRepoDto> Repositories,
    string DerivedReleaseVersion,
    Guid DerivedFromRepositoryId);

public record ReleasePreviewRepoDto(
    Guid RepositoryId,
    string Name,
    bool IsPrimary,
    bool HasChanges,
    string PreviousVersion,
    string SuggestedNextVersion,
    string BumpType,
    int CommitCount,
    int TicketCount);

public record ReleaseCompositionDto(
    Guid Id,
    Guid ProjectId,
    string Name,
    string Version,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    string? ConfluencePageUrl,
    string? NotesMarkdown,
    IReadOnlyList<ReleaseRepositoryDto> ReleaseRepositories);

public record ReleaseRepositoryDto(
    Guid Id,
    Guid RepositoryId,
    string RepositoryName,
    string PreviousVersion,
    string NextVersion,
    string BumpType,
    string FromCommitSha,
    string ToCommitSha,
    int CommitCount,
    int TicketCount,
    bool IsLegacy);

public record ReleaseSummaryDto(
    Guid Id,
    string Name,
    string Version,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PublishedAt,
    int RepoCount);
