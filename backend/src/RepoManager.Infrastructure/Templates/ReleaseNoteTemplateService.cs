using FluentValidation.Results;
using HandlebarsDotNet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Templates;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Templates;

public class ReleaseNoteTemplateService : IReleaseNoteTemplateService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ReleaseNoteTemplateService> _logger;

    public ReleaseNoteTemplateService(AppDbContext db, ILogger<ReleaseNoteTemplateService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<TemplateDto> CreateAsync(CreateTemplateDto dto, CancellationToken ct = default)
    {
        ValidateHandlebars(dto.ContentTemplate);

        var nameExists = await _db.ReleaseNoteTemplates.AnyAsync(t => t.Name == dto.Name, ct);
        if (nameExists)
            throw new ConflictException($"A template named '{dto.Name}' already exists.");

        if (dto.IsDefault)
        {
            var existing = await _db.ReleaseNoteTemplates.Where(t => t.IsDefault).ToListAsync(ct);
            foreach (var t in existing) t.IsDefault = false;
        }

        var template = new ReleaseNoteTemplate
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            ContentTemplate = dto.ContentTemplate,
            IsDefault = dto.IsDefault
        };
        _db.ReleaseNoteTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Release note template {TemplateId} created with name '{Name}'", template.Id, template.Name);
        return ToDto(template);
    }

    public async Task<IReadOnlyList<TemplateDto>> ListAsync(CancellationToken ct = default)
    {
        var templates = await _db.ReleaseNoteTemplates
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return templates.Select(ToDto).ToList();
    }

    public async Task<TemplateDto> UpdateAsync(Guid id, UpdateTemplateDto dto, CancellationToken ct = default)
    {
        var template = await _db.ReleaseNoteTemplates.FindAsync([id], ct)
            ?? throw new NotFoundException("Template", id);

        if (dto.ContentTemplate is not null)
            ValidateHandlebars(dto.ContentTemplate);

        if (dto.Name is not null && dto.Name != template.Name)
        {
            var nameExists = await _db.ReleaseNoteTemplates.AnyAsync(t => t.Name == dto.Name && t.Id != id, ct);
            if (nameExists)
                throw new ConflictException($"A template named '{dto.Name}' already exists.");
            template.Name = dto.Name;
        }
        if (dto.ContentTemplate is not null) template.ContentTemplate = dto.ContentTemplate;
        if (dto.IsDefault.HasValue)
        {
            if (dto.IsDefault.Value && !template.IsDefault)
            {
                var existing = await _db.ReleaseNoteTemplates.Where(t => t.IsDefault && t.Id != id).ToListAsync(ct);
                foreach (var t in existing) t.IsDefault = false;
            }
            template.IsDefault = dto.IsDefault.Value;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Release note template {TemplateId} updated", id);
        return ToDto(template);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _db.ReleaseNoteTemplates.FindAsync([id], ct)
            ?? throw new NotFoundException("Template", id);
        _db.ReleaseNoteTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Release note template {TemplateId} deleted", id);
    }

    public Task<TemplateDto> CloneAsync(Guid id, CancellationToken ct = default)
        => throw new NotImplementedException("CloneAsync implemented in Phase 5");

    private static void ValidateHandlebars(string content)
    {
        try { Handlebars.Compile(content); }
        catch (Exception ex)
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(content), $"Invalid Handlebars template: {ex.Message}")]);
        }
    }

    private static TemplateDto ToDto(ReleaseNoteTemplate t) =>
        new(t.Id, t.Name, t.ContentTemplate, t.IsDefault, t.IsSystem);
}
