using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Releases;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;
using ValidationException = RepoManager.Application.Common.Exceptions.ValidationException;

namespace RepoManager.Infrastructure.Releases;

public class ReleaseCompositionService : IReleaseCompositionService
{
    private readonly AppDbContext _db;
    private readonly IVersionBumpService _versionBump;
    private readonly IValidator<CreateReleaseRequest> _validator;

    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(10);

    public ReleaseCompositionService(
        AppDbContext db,
        IVersionBumpService versionBump,
        IValidator<CreateReleaseRequest> validator)
    {
        _db = db;
        _versionBump = versionBump;
        _validator = validator;
    }

    public async Task<ReleasePreviewDto> PreviewAsync(
        Guid projectId,
        IReadOnlyList<Guid> repositoryIds,
        CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.ProjectRepositories)
                .ThenInclude(pr => pr.Repository)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new NotFoundException("Project", projectId);

        var projectRepoIds = project.ProjectRepositories.Select(pr => pr.RepositoryId).ToHashSet();
        var failures = repositoryIds
            .Where(id => !projectRepoIds.Contains(id))
            .Select(id => new ValidationFailure(
                $"RepositoryIds[{id}]",
                $"Repository '{id}' does not belong to this project.")
            {
                ErrorCode = "repo_not_in_project"
            })
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        // Suggest versions for all repos in parallel
        var suggestTasks = repositoryIds.ToDictionary(
            id => id,
            id => _versionBump.SuggestAsync(id, ct));

        await Task.WhenAll(suggestTasks.Values);

        var suggestions = suggestTasks.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Result);

        // Build synthetic selections using suggested versions for DeriveReleaseVersion
        var syntheticSelections = repositoryIds
            .Select(id => new ReleaseRepositorySelectionDto(
                id,
                suggestions[id].SuggestedNextVersion,
                suggestions[id].BumpType))
            .ToList();

        var (derivedVersion, derivedFromRepoId) = DeriveReleaseVersion(syntheticSelections, project);

        var repoDtos = repositoryIds.Select(id =>
        {
            var pr = project.ProjectRepositories.First(p => p.RepositoryId == id);
            var s = suggestions[id];
            return new ReleasePreviewRepoDto(
                id,
                pr.Repository.Name,
                pr.IsPrimary,
                s.CommitCount > 0,
                s.PreviousVersion,
                s.SuggestedNextVersion,
                s.BumpType,
                s.CommitCount,
                s.TicketCount);
        }).ToList();

        return new ReleasePreviewDto(repoDtos, derivedVersion, derivedFromRepoId);
    }

    public async Task<ReleaseCompositionDto> CreateDraftAsync(
        Guid projectId,
        CreateReleaseRequest request,
        Guid createdByUserId,
        CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var project = await _db.Projects
            .Include(p => p.ProjectRepositories)
                .ThenInclude(pr => pr.Repository)
            .FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new NotFoundException("Project", projectId);

        ValidateReposMembership(request.Repositories, project);

        var snapshots = await DeriveSnapshotsAsync(request.Repositories, ct);
        var (version, _) = DeriveReleaseVersion(request.Repositories, project);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var release = new Release
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = request.Name,
            Version = version,
            Status = ReleaseStatus.Draft,
            GeneratedNotesMarkdown = string.Empty,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Releases.Add(release);

        AddReleaseRepositoryRows(release.Id, request.Repositories, snapshots);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await LoadCompositionDtoAsync(release.Id, ct);
    }

    public async Task<ReleaseCompositionDto> UpdateDraftAsync(
        Guid releaseId,
        UpdateReleaseRequest request,
        CancellationToken ct = default)
    {
        var release = await _db.Releases
            .Include(r => r.ReleaseRepositories)
            .Include(r => r.Project)
                .ThenInclude(p => p.ProjectRepositories)
                    .ThenInclude(pr => pr.Repository)
            .FirstOrDefaultAsync(r => r.Id == releaseId, ct)
            ?? throw new NotFoundException("Release", releaseId);

        if (release.Status != ReleaseStatus.Draft)
            throw new ConflictException("Release is not in Draft status.", "release_not_draft");

        ValidateUpdateRequest(request);
        ValidateReposMembership(request.Repositories, release.Project);

        var snapshots = await DeriveSnapshotsAsync(request.Repositories, ct);
        var (version, _) = DeriveReleaseVersion(request.Repositories, release.Project);

        // Wholesale replace the ReleaseRepository collection
        _db.ReleaseRepositories.RemoveRange(release.ReleaseRepositories);

        release.Version = version;

        // Refresh lock TTL on each save
        if (release.EditLockedByUserId.HasValue)
            release.EditLockExpiresAt = DateTimeOffset.UtcNow.Add(LockTtl);

        AddReleaseRepositoryRows(release.Id, request.Repositories, snapshots);

        await _db.SaveChangesAsync(ct);

        return await LoadCompositionDtoAsync(release.Id, ct);
    }

    public async Task<ReleaseCompositionDto> GetAsync(Guid releaseId, CancellationToken ct = default)
    {
        _ = await _db.Releases.FindAsync([releaseId], ct)
            ?? throw new NotFoundException("Release", releaseId);

        return await LoadCompositionDtoAsync(releaseId, ct);
    }

    public async Task<string?> TryAcquireEditLockAsync(
        Guid releaseId,
        Guid userId,
        string userName,
        CancellationToken ct = default)
    {
        var release = await _db.Releases.FindAsync([releaseId], ct)
            ?? throw new NotFoundException("Release", releaseId);

        var now = DateTimeOffset.UtcNow;

        // Lock is free if: no holder, holder is current user, or TTL expired
        if (release.EditLockedByUserId.HasValue
            && release.EditLockedByUserId != userId
            && release.EditLockExpiresAt > now)
        {
            return release.EditLockedByUserName ?? "another user";
        }

        release.EditLockedByUserId = userId;
        release.EditLockedByUserName = userName;
        release.EditLockExpiresAt = now.Add(LockTtl);

        await _db.SaveChangesAsync(ct);
        return null;
    }

    public async Task ReleaseEditLockAsync(Guid releaseId, Guid userId, CancellationToken ct = default)
    {
        var release = await _db.Releases.FindAsync([releaseId], ct)
            ?? throw new NotFoundException("Release", releaseId);

        if (release.EditLockedByUserId == userId)
        {
            release.EditLockedByUserId = null;
            release.EditLockedByUserName = null;
            release.EditLockExpiresAt = null;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteDraftAsync(Guid releaseId, CancellationToken ct = default)
    {
        var release = await _db.Releases.FindAsync([releaseId], ct)
            ?? throw new NotFoundException("Release", releaseId);

        if (release.Status != ReleaseStatus.Draft)
            throw new ConflictException("Only Draft releases can be deleted.", "release_not_draft");

        _db.Releases.Remove(release);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ReleaseSummaryDto>> ListByProjectAsync(
        Guid projectId,
        string? status,
        string? search,
        string? sort,
        string? order,
        CancellationToken ct = default)
    {
        _ = await _db.Projects.FindAsync([projectId], ct)
            ?? throw new NotFoundException("Project", projectId);

        var query = _db.Releases
            .Where(r => r.ProjectId == projectId)
            .Include(r => r.ReleaseRepositories)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ReleaseStatus>(status, true, out var statusEnum))
            query = query.Where(r => r.Status == statusEnum);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(r => r.Name.ToLower().Contains(search.ToLower()));

        query = (sort?.ToLower(), order?.ToLower()) switch
        {
            ("name", "asc") => query.OrderBy(r => r.Name),
            ("name", "desc") => query.OrderByDescending(r => r.Name),
            ("createdat", "asc") => query.OrderBy(r => r.CreatedAt),
            _ => query.OrderByDescending(r => r.CreatedAt)
        };

        var releases = await query.ToListAsync(ct);

        return releases.Select(r => new ReleaseSummaryDto(
            r.Id,
            r.Name,
            r.Version,
            r.Status.ToString(),
            r.CreatedAt,
            r.PublishedAt,
            r.ReleaseRepositories.Count)).ToList();
    }

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

    private static void ValidateReposMembership(
        IReadOnlyList<ReleaseRepositorySelectionDto> selections,
        Project project)
    {
        var projectRepoIds = project.ProjectRepositories.Select(pr => pr.RepositoryId).ToHashSet();
        var failures = selections
            .Where(s => !projectRepoIds.Contains(s.RepositoryId))
            .Select(s => new ValidationFailure(
                $"Repositories[{s.RepositoryId}]",
                $"Repository '{s.RepositoryId}' does not belong to this project.")
            {
                ErrorCode = "repo_not_in_project"
            })
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);
    }

    private static void ValidateUpdateRequest(UpdateReleaseRequest request)
    {
        var failures = new List<ValidationFailure>();

        if (request.Repositories is null || request.Repositories.Count == 0)
        {
            failures.Add(new ValidationFailure("Repositories", "At least one repository must be selected.")
            {
                ErrorCode = "at_least_one_repo_required"
            });
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);
    }

    private async Task<Dictionary<Guid, VersionBumpSuggestionDto>> DeriveSnapshotsAsync(
        IReadOnlyList<ReleaseRepositorySelectionDto> selections,
        CancellationToken ct)
    {
        var result = new Dictionary<Guid, VersionBumpSuggestionDto>();
        foreach (var sel in selections)
        {
            result[sel.RepositoryId] = await _versionBump.SuggestAsync(sel.RepositoryId, ct);
        }
        return result;
    }

    private void AddReleaseRepositoryRows(
        Guid releaseId,
        IReadOnlyList<ReleaseRepositorySelectionDto> selections,
        Dictionary<Guid, VersionBumpSuggestionDto> snapshots)
    {
        foreach (var sel in selections)
        {
            var snap = snapshots.TryGetValue(sel.RepositoryId, out var s) ? s : null;
            _db.ReleaseRepositories.Add(new ReleaseRepository
            {
                Id = Guid.NewGuid(),
                ReleaseId = releaseId,
                RepositoryId = sel.RepositoryId,
                NextVersion = sel.NextVersion,
                BumpType = sel.BumpType,
                PreviousVersion = snap?.PreviousVersion ?? string.Empty,
                FromCommitSha = snap?.FromCommitSha ?? string.Empty,
                ToCommitSha = snap?.ToCommitSha ?? string.Empty,
                CommitCount = snap?.CommitCount ?? 0,
                TicketCount = snap?.TicketCount ?? 0
            });
        }
    }

    private async Task<ReleaseCompositionDto> LoadCompositionDtoAsync(Guid releaseId, CancellationToken ct)
    {
        var release = await _db.Releases
            .Include(r => r.ReleaseRepositories)
                .ThenInclude(rr => rr.Repository)
            .FirstAsync(r => r.Id == releaseId, ct);

        return ToDto(release);
    }

    private static ReleaseCompositionDto ToDto(Release r)
    {
        var repos = r.ReleaseRepositories.Select(rr => new ReleaseRepositoryDto(
            rr.Id,
            rr.RepositoryId,
            rr.Repository?.Name ?? string.Empty,
            rr.PreviousVersion,
            rr.NextVersion,
            rr.BumpType,
            rr.FromCommitSha,
            rr.ToCommitSha,
            rr.CommitCount,
            rr.TicketCount,
            IsLegacy: string.IsNullOrEmpty(rr.PreviousVersion)
                   && string.IsNullOrEmpty(rr.FromCommitSha)
                   && string.IsNullOrEmpty(rr.ToCommitSha)
                   && rr.CommitCount == 0)).ToList();

        return new ReleaseCompositionDto(
            r.Id,
            r.ProjectId,
            r.Name,
            r.Version,
            r.Status.ToString(),
            r.CreatedAt,
            r.PublishedAt,
            r.ConfluencePageUrl,
            r.EditedNotesMarkdown ?? r.GeneratedNotesMarkdown,
            repos);
    }
}
