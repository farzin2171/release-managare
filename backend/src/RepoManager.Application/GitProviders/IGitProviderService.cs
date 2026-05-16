using RepoManager.Domain.ValueObjects;

namespace RepoManager.Application.GitProviders;

public interface IGitProviderService
{
    Task<IReadOnlyList<RepositoryTag>> ListTagsAsync(Guid repositoryId, CancellationToken ct = default);
}
