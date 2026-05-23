using RepoManager.Application.Releases;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Releases;

public class ReleaseCompositionService : IReleaseCompositionService
{
    private readonly AppDbContext _db;
    private readonly IVersionBumpService _versionBump;

    public ReleaseCompositionService(AppDbContext db, IVersionBumpService versionBump)
    {
        _db = db;
        _versionBump = versionBump;
    }

    public Task<ReleasePreviewDto> PreviewAsync(
        Guid projectId,
        IReadOnlyList<Guid> repositoryIds,
        CancellationToken ct = default)
        => throw new NotImplementedException("Implemented in Phase 3 (T024)");

    public Task<ReleaseCompositionDto> CreateDraftAsync(
        Guid projectId,
        CreateReleaseRequest request,
        CancellationToken ct = default)
        => throw new NotImplementedException("Implemented in Phase 3 (T014)");

    public Task<ReleaseCompositionDto> UpdateDraftAsync(
        Guid releaseId,
        UpdateReleaseRequest request,
        CancellationToken ct = default)
        => throw new NotImplementedException("Implemented in Phase 3 (T014)");

    // Pure function: derives the release-level version from the selected repos.
    // Uses the primary repo's NextVersion when it is included in the selection;
    // otherwise falls back to the alphabetically-first selected repo.
    internal static (string Version, Guid RepoId) DeriveReleaseVersion(
        IEnumerable<ReleaseRepositorySelectionDto> selections,
        Project project)
    {
        var selectionList = selections.ToList();

        var primaryRepoId = project.ProjectRepositories
            .FirstOrDefault(pr => pr.IsPrimary)?.RepositoryId;

        if (primaryRepoId.HasValue)
        {
            var primary = selectionList.FirstOrDefault(s => s.RepositoryId == primaryRepoId.Value);
            if (primary is not null)
                return (primary.NextVersion, primary.RepositoryId);
        }

        var fallback = selectionList
            .Join(
                project.ProjectRepositories,
                s => s.RepositoryId,
                pr => pr.RepositoryId,
                (s, pr) => new { Selection = s, Repo = pr.Repository })
            .OrderBy(x => x.Repo.Name, StringComparer.OrdinalIgnoreCase)
            .First();

        return (fallback.Selection.NextVersion, fallback.Selection.RepositoryId);
    }
}
