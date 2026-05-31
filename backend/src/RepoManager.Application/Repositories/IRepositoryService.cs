using RepoManager.Domain.ValueObjects;

namespace RepoManager.Application.Repositories;

public interface IRepositoryService
{
    Task<IReadOnlyList<RepositoryDto>> ListAsync(ListRepositoriesQuery query, CancellationToken ct = default);
    Task<RepositoryDto> SetTrackedAsync(Guid id, SetTrackedDto dto, CancellationToken ct = default);
    Task<RepositoryDto> UpdateAsync(Guid id, UpdateRepositoryRequest dto, CancellationToken ct = default);
    Task<RepositoryChangesDto> GetChangesAsync(Guid repositoryId, GetChangesQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<RepositoryTag>> GetTagsAsync(Guid repositoryId, CancellationToken ct = default);
    Task<RepositoryDto> SetLatestTagAsync(Guid repositoryId, string tagName, Guid actingUserId, CancellationToken ct = default);
    Task ClearLatestTagAsync(Guid repositoryId, Guid actingUserId, CancellationToken ct = default);
}

public record ListRepositoriesQuery(
    Guid? ConnectionId,
    bool? IsTracked,
    string? Search);

public record SetTrackedDto(bool IsTracked);

public record UserSummaryDto(Guid Id, string Email);

public record RepositoryDto(
    Guid Id,
    Guid GitProviderConnectionId,
    string ExternalId,
    string Name,
    string DefaultBranch,
    string WebUrl,
    string AzureProjectName,
    bool IsTracked,
    string? ServiceOwner,
    DateTimeOffset? LastSyncedAt,
    string? LatestTag,
    string? LatestTagCommitSha,
    DateTime? LatestTagSetAt,
    UserSummaryDto? LatestTagSetBy);

public record UpdateRepositoryRequest(string? ServiceOwner);

public record GetChangesQuery(
    string GroupBy = "ticket",
    string? Type = null,
    string? Contributor = null,
    string? Search = null);

public record RepositoryChangesDto(
    Guid RepositoryId,
    string RepositoryName,
    string FromTag,
    string ToTag,
    ChangeSummaryDto Summary,
    IReadOnlyList<ChangeGroupDto> Groups,
    IReadOnlyList<CommitItemDto> Unscoped);

public record ChangeSummaryDto(
    int CommitCount,
    int TicketCount,
    int BreakingCount,
    int ContributorCount);

public record ChangeGroupDto(
    string Key,
    string? Title,
    string? Type,
    bool IsBreaking,
    int CommitCount,
    int ContributorCount,
    IReadOnlyList<CommitItemDto> Commits);

public record CommitItemDto(
    string Sha,
    string ShortSha,
    string Message,
    string Author,
    DateTimeOffset CommittedAt);
