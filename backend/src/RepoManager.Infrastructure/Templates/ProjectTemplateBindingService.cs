using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.DTOs.Bindings;
using RepoManager.Application.Services;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Templates;

public class ProjectTemplateBindingService : IProjectTemplateBindingService
{
    private readonly AppDbContext _db;
    private readonly IValidator<CreateBindingRequest> _createValidator;
    private readonly IValidator<UpdateBindingRequest> _updateValidator;

    public ProjectTemplateBindingService(
        AppDbContext db,
        IValidator<CreateBindingRequest> createValidator,
        IValidator<UpdateBindingRequest> updateValidator)
    {
        _db = db;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<ProjectTemplateBindingDto>> GetAllAsync(
        Guid projectId, CancellationToken ct = default)
    {
        _ = await _db.Projects.FindAsync([projectId], ct)
            ?? throw new NotFoundException("Project", projectId);

        var bindings = await _db.TemplateBindings
            .Where(b => b.ProjectId == projectId)
            .Include(b => b.Template)
            .OrderBy(b => b.SortOrder)
            .ToListAsync(ct);

        return bindings.Select(ToDto).ToList();
    }

    public async Task<ProjectTemplateBindingDto> CreateAsync(
        Guid projectId, CreateBindingRequest request, CancellationToken ct = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);

        _ = await _db.Projects.FindAsync([projectId], ct)
            ?? throw new NotFoundException("Project", projectId);

        if (Enum.Parse<TemplateBindingKind>(request.Kind) == TemplateBindingKind.ReleaseNotes)
        {
            var existing = await _db.TemplateBindings
                .AnyAsync(b => b.ProjectId == projectId && b.Kind == TemplateBindingKind.ReleaseNotes, ct);
            if (existing)
                throw new ConflictException(
                    "Project already has a ReleaseNotes binding.",
                    "duplicate_release_notes_binding");
        }

        var template = await _db.ReleaseNoteTemplates.FindAsync([request.TemplateId], ct)
            ?? throw new NotFoundException("ReleaseNoteTemplate", request.TemplateId);

        var binding = new ProjectTemplateBinding
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TemplateId = request.TemplateId,
            Kind = Enum.Parse<TemplateBindingKind>(request.Kind),
            PageTitleTemplate = request.PageTitleTemplate,
            ParentPageId = request.ParentPageId,
            LinkFromReleaseNotes = request.LinkFromReleaseNotes,
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _db.TemplateBindings.Add(binding);
        await _db.SaveChangesAsync(ct);

        binding.Template = template;
        return ToDto(binding);
    }

    public async Task<ProjectTemplateBindingDto> UpdateAsync(
        Guid projectId, Guid bindingId, UpdateBindingRequest request, CancellationToken ct = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);

        var binding = await _db.TemplateBindings
            .Include(b => b.Template)
            .FirstOrDefaultAsync(b => b.Id == bindingId && b.ProjectId == projectId, ct)
            ?? throw new NotFoundException("ProjectTemplateBinding", bindingId);

        if (request.TemplateId.HasValue)
        {
            _ = await _db.ReleaseNoteTemplates.FindAsync([request.TemplateId.Value], ct)
                ?? throw new NotFoundException("ReleaseNoteTemplate", request.TemplateId.Value);
            binding.TemplateId = request.TemplateId.Value;
        }

        if (request.Kind is not null)
            binding.Kind = Enum.Parse<TemplateBindingKind>(request.Kind);

        if (request.PageTitleTemplate is not null)
            binding.PageTitleTemplate = request.PageTitleTemplate;

        if (request.ParentPageId is not null)
            binding.ParentPageId = request.ParentPageId;

        if (request.LinkFromReleaseNotes.HasValue)
            binding.LinkFromReleaseNotes = request.LinkFromReleaseNotes.Value;

        if (request.SortOrder.HasValue)
            binding.SortOrder = request.SortOrder.Value;

        binding.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        // Re-load template if changed
        if (request.TemplateId.HasValue)
        {
            await _db.Entry(binding).Reference(b => b.Template).LoadAsync(ct);
        }

        return ToDto(binding);
    }

    public async Task DeleteAsync(
        Guid projectId, Guid bindingId, CancellationToken ct = default)
    {
        var binding = await _db.TemplateBindings
            .FirstOrDefaultAsync(b => b.Id == bindingId && b.ProjectId == projectId, ct)
            ?? throw new NotFoundException("ProjectTemplateBinding", bindingId);

        if (binding.Kind == TemplateBindingKind.ReleaseNotes)
        {
            var releaseNotesCount = await _db.TemplateBindings
                .CountAsync(b => b.ProjectId == projectId && b.Kind == TemplateBindingKind.ReleaseNotes, ct);

            if (releaseNotesCount <= 1)
                throw new ConflictException(
                    "Cannot delete the last ReleaseNotes binding.",
                    "last_release_notes_binding");
        }

        _db.TemplateBindings.Remove(binding);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectTemplateBindingDto>> ReorderAsync(
        Guid projectId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default)
    {
        _ = await _db.Projects.FindAsync([projectId], ct)
            ?? throw new NotFoundException("Project", projectId);

        var bindings = await _db.TemplateBindings
            .Include(b => b.Template)
            .Where(b => b.ProjectId == projectId)
            .ToListAsync(ct);

        var currentIds = bindings.Select(b => b.Id).ToHashSet();
        if (orderedIds.Count != currentIds.Count || orderedIds.Any(id => !currentIds.Contains(id)))
        {
            throw new Application.Common.Exceptions.ValidationException([
                new FluentValidation.Results.ValidationFailure(
                    "OrderedIds",
                    "Ordered IDs must match the project's current binding set exactly.")
                {
                    ErrorCode = "invalid_ordered_ids"
                }
            ]);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        for (var i = 0; i < orderedIds.Count; i++)
        {
            var binding = bindings.First(b => b.Id == orderedIds[i]);
            binding.SortOrder = i;
            binding.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return bindings.OrderBy(b => b.SortOrder).Select(ToDto).ToList();
    }

    private static ProjectTemplateBindingDto ToDto(ProjectTemplateBinding b) =>
        new(b.Id,
            b.ProjectId,
            b.TemplateId,
            b.Template?.Name ?? string.Empty,
            b.Kind.ToString(),
            b.PageTitleTemplate,
            b.ParentPageId,
            b.LinkFromReleaseNotes,
            b.SortOrder);
}
