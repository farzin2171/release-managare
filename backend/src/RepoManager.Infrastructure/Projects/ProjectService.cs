using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Projects;
using RepoManager.Application.Repositories;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Projects;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _db;
    private readonly IRepositoryService _repositoryService;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(AppDbContext db, IRepositoryService repositoryService, ILogger<ProjectService> logger)
    {
        _db = db;
        _repositoryService = repositoryService;
        _logger = logger;
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectDto dto, CancellationToken ct = default)
    {
        var nameExists = await _db.Projects.AnyAsync(p => p.Name == dto.Name, ct);
        if (nameExists)
            throw new ConflictException($"A project named '{dto.Name}' already exists.");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            Color = dto.Color,
            JiraProjectKeys = "[\"DIT\"]",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Project {ProjectId} created with name '{Name}'", project.Id, project.Name);
        return ToDto(project, []);
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct = default)
    {
        var projects = await _db.Projects
            .Include(p => p.ProjectRepositories)
                .ThenInclude(pr => pr.Repository)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
        return projects.Select(p => ToDto(p, p.ProjectRepositories)).ToList();
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.ProjectRepositories)
                .ThenInclude(pr => pr.Repository)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);
        return ToDto(project, project.ProjectRepositories);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectDto dto, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.ProjectRepositories)
                .ThenInclude(pr => pr.Repository)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        if (dto.Name is not null && dto.Name != project.Name)
        {
            var nameExists = await _db.Projects.AnyAsync(p => p.Name == dto.Name && p.Id != id, ct);
            if (nameExists)
                throw new ConflictException($"A project named '{dto.Name}' already exists.");
            project.Name = dto.Name;
        }
        if (dto.Description is not null) project.Description = dto.Description;
        if (dto.Color is not null) project.Color = dto.Color;
        if (dto.ConfluenceSpaceKey is not null) project.ConfluenceSpaceKey = dto.ConfluenceSpaceKey;
        if (dto.ConfluenceParentPageId is not null) project.ConfluenceParentPageId = dto.ConfluenceParentPageId;

        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Project {ProjectId} updated", id);
        return ToDto(project, project.ProjectRepositories);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _db.Projects.FindAsync([id], ct)
            ?? throw new NotFoundException("Project", id);
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Project {ProjectId} deleted", id);
    }

    public async Task<ProjectDto> AssignRepositoryAsync(Guid id, Guid repoId, AssignRepositoryDto dto, CancellationToken ct = default)
    {
        _ = await _db.Repositories.FindAsync([repoId], ct)
            ?? throw new NotFoundException("Repository", repoId);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var project = await _db.Projects
            .Include(p => p.ProjectRepositories)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        if (dto.IsPrimary)
        {
            foreach (var pr in project.ProjectRepositories.Where(pr => pr.IsPrimary))
                pr.IsPrimary = false;
        }

        var existing = project.ProjectRepositories.FirstOrDefault(pr => pr.RepositoryId == repoId);
        if (existing is not null)
        {
            existing.IsPrimary = dto.IsPrimary;
        }
        else
        {
            _db.ProjectRepositories.Add(new ProjectRepository
            {
                ProjectId = id,
                RepositoryId = repoId,
                IsPrimary = dto.IsPrimary
            });
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        _logger.LogInformation("Repository {RepoId} assigned to project {ProjectId} (isPrimary={IsPrimary})", repoId, id, dto.IsPrimary);
        return await LoadProjectDtoAsync(id, ct);
    }

    public async Task<ProjectDto> RemoveRepositoryAsync(Guid id, Guid repoId, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.ProjectRepositories)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        var link = project.ProjectRepositories.FirstOrDefault(pr => pr.RepositoryId == repoId)
            ?? throw new NotFoundException("ProjectRepository", repoId);

        // Block removal if the repo is included in any Draft release for this project
        var inDraftRelease = await _db.ReleaseRepositories
            .AnyAsync(rr => rr.RepositoryId == repoId
                         && rr.Release.ProjectId == id
                         && rr.Release.Status == Domain.Enums.ReleaseStatus.Draft, ct);

        if (inDraftRelease)
            throw new Application.Common.Exceptions.ValidationException([
                new FluentValidation.Results.ValidationFailure("RepositoryId",
                    "This repository is included in one or more Draft releases for this project.")
                {
                    ErrorCode = "repo_in_draft_release"
                }
            ]);

        _db.ProjectRepositories.Remove(link);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Repository {RepoId} removed from project {ProjectId}", repoId, id);
        return await LoadProjectDtoAsync(id, ct);
    }

    public async Task<ProjectDto> ConfigureJiraAsync(Guid id, ConfigureJiraDto dto, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.ProjectRepositories)
                .ThenInclude(pr => pr.Repository)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        if (dto.JiraConnectionId.HasValue)
        {
            var jiraExists = await _db.JiraConnections.AnyAsync(j => j.Id == dto.JiraConnectionId.Value, ct);
            if (!jiraExists)
                throw new NotFoundException("JiraConnection", dto.JiraConnectionId.Value);
        }

        project.JiraConnectionId = dto.JiraConnectionId;
        project.JiraProjectKeys = "[\"DIT\"]";
        project.FixVersionPattern = dto.FixVersionPattern;
        project.AutoCreateFixVersion = dto.AutoCreateFixVersion;
        project.MatchSubtasksToParents = dto.MatchSubtasksToParents;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Jira configured for project {ProjectId}", id);
        return ToDto(project, project.ProjectRepositories);
    }

    public async Task<ProjectChangesDto> GetChangesAsync(Guid id, GetChangesQuery query, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(p => p.ProjectRepositories)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Project", id);

        var repoIds = project.ProjectRepositories.Select(pr => pr.RepositoryId).ToList();
        if (repoIds.Count == 0)
            return new ProjectChangesDto(id, project.Name, new ChangeSummaryDto(0, 0, 0, 0), [], [], []);

        var repoNames = await _db.Repositories
            .Where(r => repoIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, ct);

        var results = await Task.WhenAll(
            repoIds.Select(repoId => _repositoryService.GetChangesAsync(repoId, query, ct)));

        var dto = query.GroupBy.ToLowerInvariant() switch
        {
            "contributor" => AggregateByContributor(id, project.Name, results, repoNames),
            "commit"      => AggregateFlat(id, project.Name, results),
            _             => AggregateByTicket(id, project.Name, results, repoNames)
        };
        return dto with { Repositories = results };
    }

    private static ProjectChangesDto AggregateByTicket(
        Guid pid, string pname, RepositoryChangesDto[] results, Dictionary<Guid, string> repoNames)
    {
        var merged = MergeGroups(results, repoNames);
        var groups = merged.Select(kv =>
        {
            var (title, type, breaking, commits, repos) = kv.Value;
            return new ProjectChangeGroupDto(kv.Key, title, type, breaking,
                commits.Count,
                commits.Select(c => c.Author).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                repos, commits);
        }).ToList();
        var unscoped = results.SelectMany(r => r.Unscoped).ToList();
        return new ProjectChangesDto(pid, pname,
            new ChangeSummaryDto(results.Sum(r => r.Summary.CommitCount), groups.Count,
                results.Sum(r => r.Summary.BreakingCount), results.Sum(r => r.Summary.ContributorCount)),
            groups, unscoped);
    }

    private static ProjectChangesDto AggregateByContributor(
        Guid pid, string pname, RepositoryChangesDto[] results, Dictionary<Guid, string> repoNames)
    {
        var merged = MergeGroups(results, repoNames);
        var groups = merged
            .OrderByDescending(kv => kv.Value.Commits.Count)
            .Select(kv =>
            {
                var (title, _, breaking, commits, repos) = kv.Value;
                return new ProjectChangeGroupDto(kv.Key, title, null, breaking,
                    commits.Count, 1, repos, commits);
            }).ToList();
        return new ProjectChangesDto(pid, pname,
            new ChangeSummaryDto(results.Sum(r => r.Summary.CommitCount), 0,
                results.Sum(r => r.Summary.BreakingCount), groups.Count),
            groups, []);
    }

    private static ProjectChangesDto AggregateFlat(
        Guid pid, string pname, RepositoryChangesDto[] results)
    {
        var commits = results.SelectMany(r => r.Unscoped).ToList();
        return new ProjectChangesDto(pid, pname,
            new ChangeSummaryDto(commits.Count, 0, 0,
                commits.Select(c => c.Author).Distinct(StringComparer.OrdinalIgnoreCase).Count()),
            [], commits);
    }

    private static Dictionary<string, (string? Title, string? Type, bool IsBreaking, List<CommitItemDto> Commits, List<string> Repos)>
        MergeGroups(RepositoryChangesDto[] results, Dictionary<Guid, string> repoNames)
    {
        var dict = new Dictionary<string, (string? Title, string? Type, bool IsBreaking, List<CommitItemDto> Commits, List<string> Repos)>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in results)
        {
            var name = repoNames.GetValueOrDefault(r.RepositoryId, r.RepositoryName);
            foreach (var g in r.Groups)
            {
                if (dict.TryGetValue(g.Key, out var e))
                {
                    e.Commits.AddRange(g.Commits);
                    if (!e.Repos.Contains(name)) e.Repos.Add(name);
                    dict[g.Key] = (e.Title ?? g.Title, e.Type ?? g.Type, e.IsBreaking || g.IsBreaking, e.Commits, e.Repos);
                }
                else
                    dict[g.Key] = (g.Title, g.Type, g.IsBreaking, [.. g.Commits], [name]);
            }
        }
        return dict;
    }

    private async Task<ProjectDto> LoadProjectDtoAsync(Guid id, CancellationToken ct)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Include(p => p.ProjectRepositories)
                .ThenInclude(pr => pr.Repository)
            .FirstAsync(p => p.Id == id, ct);
        return ToDto(project, project.ProjectRepositories);
    }

    private static ProjectDto ToDto(Project p, IEnumerable<ProjectRepository> projectRepos)
    {
        var keys = string.IsNullOrEmpty(p.JiraProjectKeys)
            ? Array.Empty<string>()
            : JsonSerializer.Deserialize<string[]>(p.JiraProjectKeys) ?? [];

        var repos = projectRepos
            .Select(pr => new ProjectRepositoryDto(
                pr.RepositoryId,
                pr.Repository?.Name ?? string.Empty,
                pr.Repository?.DefaultBranch ?? string.Empty,
                pr.IsPrimary))
            .ToList();

        return new ProjectDto(
            p.Id, p.Name, p.Description, p.Color,
            p.ConfluenceSpaceKey, p.ConfluenceParentPageId,
            p.JiraConnectionId, keys, p.FixVersionPattern,
            p.AutoCreateFixVersion, p.MatchSubtasksToParents,
            p.CreatedAt, p.UpdatedAt, repos);
    }
}
