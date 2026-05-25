using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.DTOs.CustomVariables;
using RepoManager.Application.Services;
using RepoManager.Application.Validators;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Templates;

public class ProjectCustomVariableService : IProjectCustomVariableService
{
    private readonly AppDbContext _db;
    private readonly ProjectCustomVariableUpsertValidator _validator;

    public ProjectCustomVariableService(AppDbContext db, ProjectCustomVariableUpsertValidator validator)
    {
        _db = db;
        _validator = validator;
    }

    public async Task<IReadOnlyList<ProjectCustomVariableDto>> GetAllAsync(
        Guid projectId, CancellationToken ct = default)
    {
        _ = await _db.Projects.FindAsync([projectId], ct)
            ?? throw new NotFoundException("Project", projectId);

        var vars = await _db.CustomVariables
            .Where(v => v.ProjectId == projectId)
            .OrderBy(v => v.Key)
            .ToListAsync(ct);

        return vars.Select(v => new ProjectCustomVariableDto(v.Key, v.Value)).ToList();
    }

    public async Task<ProjectCustomVariableDto> UpsertAsync(
        Guid projectId, string key, string value, CancellationToken ct = default)
    {
        await _validator.ValidateAndThrowAsync((key, value), ct);

        _ = await _db.Projects.FindAsync([projectId], ct)
            ?? throw new NotFoundException("Project", projectId);

        var existing = await _db.CustomVariables
            .FirstOrDefaultAsync(v => v.ProjectId == projectId && v.Key == key, ct);

        if (existing is not null)
        {
            existing.Value = value;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            _db.CustomVariables.Add(new ProjectCustomVariable
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Key = key,
                Value = value,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
        return new ProjectCustomVariableDto(key, value);
    }

    public async Task DeleteAsync(
        Guid projectId, string key, CancellationToken ct = default)
    {
        var variable = await _db.CustomVariables
            .FirstOrDefaultAsync(v => v.ProjectId == projectId && v.Key == key, ct)
            ?? throw new NotFoundException("ProjectCustomVariable", key);

        _db.CustomVariables.Remove(variable);
        await _db.SaveChangesAsync(ct);
    }
}
