using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Projects;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Projects;

public class ProjectService : IProjectService
{
    private readonly AppDbContext _db;

    public ProjectService(AppDbContext db) => _db = db;

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
            JiraProjectKeys = "[]",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);
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
        if (dto.ReleaseNoteTemplateId.HasValue) project.ReleaseNoteTemplateId = dto.ReleaseNoteTemplateId;
        if (dto.ConfluenceSpaceKey is not null) project.ConfluenceSpaceKey = dto.ConfluenceSpaceKey;
        if (dto.ConfluenceParentPageId is not null) project.ConfluenceParentPageId = dto.ConfluenceParentPageId;

        project.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(project, project.ProjectRepositories);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var project = await _db.Projects.FindAsync([id], ct)
            ?? throw new NotFoundException("Project", id);
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);
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

        _db.ProjectRepositories.Remove(link);
        await _db.SaveChangesAsync(ct);
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
        project.JiraProjectKeys = JsonSerializer.Serialize(dto.JiraProjectKeys);
        project.FixVersionPattern = dto.FixVersionPattern;
        project.AutoCreateFixVersion = dto.AutoCreateFixVersion;
        project.MatchSubtasksToParents = dto.MatchSubtasksToParents;
        project.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return ToDto(project, project.ProjectRepositories);
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
            p.ReleaseNoteTemplateId, p.ConfluenceSpaceKey, p.ConfluenceParentPageId,
            p.JiraConnectionId, keys, p.FixVersionPattern,
            p.AutoCreateFixVersion, p.MatchSubtasksToParents,
            p.CreatedAt, p.UpdatedAt, repos);
    }
}
