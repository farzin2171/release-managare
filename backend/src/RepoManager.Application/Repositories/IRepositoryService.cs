namespace RepoManager.Application.Repositories;

public interface IRepositoryService
{
    Task<IReadOnlyList<RepositoryDto>> ListAsync(ListRepositoriesQuery query, CancellationToken ct = default);
    Task<RepositoryDto> SetTrackedAsync(Guid id, SetTrackedDto dto, CancellationToken ct = default);
}

public record ListRepositoriesQuery(
    Guid? ConnectionId,
    bool? IsTracked,
    string? Search);

public record SetTrackedDto(bool IsTracked);

public record RepositoryDto(
    Guid Id,
    Guid GitProviderConnectionId,
    string ExternalId,
    string Name,
    string DefaultBranch,
    string WebUrl,
    string AzureProjectName,
    bool IsTracked,
    DateTimeOffset? LastSyncedAt);
