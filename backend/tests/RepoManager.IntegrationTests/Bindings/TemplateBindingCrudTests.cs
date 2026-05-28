using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.DTOs.Bindings;
using RepoManager.Application.Validators;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;
using RepoManager.Infrastructure.Templates;
using ValidationException = RepoManager.Application.Common.Exceptions.ValidationException;

namespace RepoManager.IntegrationTests.Bindings;

public class TemplateBindingCrudTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ProjectTemplateBindingService _service;

    private readonly Guid _projectId;
    private readonly Guid _templateId;

    public TemplateBindingCrudTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _service = new ProjectTemplateBindingService(
            _db,
            new CreateBindingRequestValidator(),
            new UpdateBindingRequestValidator());

        (_projectId, _templateId) = SeedProjectAndTemplate().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_ValidReleaseNotesBinding_PersistsAndReturnsDto()
    {
        var req = new CreateBindingRequest(_templateId, "ReleaseNotes", "{{project.name}} Release Notes", null, false, 0);
        var dto = await _service.CreateAsync(_projectId, req);

        dto.Id.Should().NotBeEmpty();
        dto.Kind.Should().Be("ReleaseNotes");
        dto.PageTitleTemplate.Should().Be("{{project.name}} Release Notes");
        dto.TemplateName.Should().NotBeEmpty();

        var dbRow = await _db.TemplateBindings.FindAsync([dto.Id]);
        dbRow.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_SecondReleaseNotesBinding_ThrowsConflict()
    {
        await _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "ReleaseNotes", "First", null, false, 0));

        var act = () => _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "ReleaseNotes", "Second", null, false, 1));

        await act.Should().ThrowAsync<ConflictException>()
            .Where(e => e.Code == "duplicate_release_notes_binding");
    }

    [Fact]
    public async Task Create_MultipleChecklistBindings_Allowed()
    {
        await _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "Checklist", "Checklist A", null, false, 1));
        await _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "Checklist", "Checklist B", null, false, 2));

        var all = await _service.GetAllAsync(_projectId);
        all.Where(b => b.Kind == "Checklist").Should().HaveCount(2);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ValidRequest_PersistsChanges()
    {
        var created = await _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "ReleaseNotes", "Original title", null, false, 0));

        var updated = await _service.UpdateAsync(_projectId, created.Id,
            new UpdateBindingRequest(null, null, "Updated title", null, true, null));

        updated.PageTitleTemplate.Should().Be("Updated title");
        updated.LinkFromReleaseNotes.Should().BeTrue();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_LastReleaseNotesBinding_ThrowsConflict()
    {
        var created = await _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "ReleaseNotes", "Only binding", null, false, 0));

        var act = () => _service.DeleteAsync(_projectId, created.Id);

        await act.Should().ThrowAsync<ConflictException>()
            .Where(e => e.Code == "last_release_notes_binding");
    }

    [Fact]
    public async Task Delete_ChecklistBinding_Succeeds()
    {
        await _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "ReleaseNotes", "RN", null, false, 0));
        var cl = await _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "Checklist", "CL", null, false, 1));

        await _service.DeleteAsync(_projectId, cl.Id);

        var all = await _service.GetAllAsync(_projectId);
        all.Should().HaveCount(1);
        all[0].Kind.Should().Be("ReleaseNotes");
    }

    // ── Reorder ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reorder_ValidOrderedIds_UpdatesSortOrders()
    {
        var rn = await _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "ReleaseNotes", "RN", null, false, 0));
        var cl = await _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "Checklist", "CL", null, false, 1));

        // Swap: checklist first, release notes second
        var result = await _service.ReorderAsync(_projectId, [cl.Id, rn.Id]);

        result[0].Id.Should().Be(cl.Id);
        result[0].SortOrder.Should().Be(0);
        result[1].Id.Should().Be(rn.Id);
        result[1].SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task Reorder_MismatchedIds_ThrowsValidationException()
    {
        await _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "ReleaseNotes", "RN", null, false, 0));

        // Provide one unknown ID instead of the actual binding IDs
        var act = () => _service.ReorderAsync(_projectId, [Guid.NewGuid()]);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Failures.Any(f => f.ErrorCode == "invalid_ordered_ids"));
    }

    [Fact]
    public async Task Reorder_WrongCount_ThrowsValidationException()
    {
        var rn = await _service.CreateAsync(_projectId,
            new CreateBindingRequest(_templateId, "ReleaseNotes", "RN", null, false, 0));

        // One binding exists; provide two IDs → count mismatch
        var act = () => _service.ReorderAsync(_projectId, [rn.Id, Guid.NewGuid()]);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Failures.Any(f => f.ErrorCode == "invalid_ordered_ids"));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<(Guid projectId, Guid templateId)> SeedProjectAndTemplate()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Test-{Guid.NewGuid():N}",
            Color = "#3B82F6",
            JiraProjectKeys = "[]",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Projects.Add(project);

        var template = new ReleaseNoteTemplate
        {
            Id = Guid.NewGuid(),
            Name = $"Tmpl-{Guid.NewGuid():N}",
            ContentTemplate = "# Release Notes",
            IsDefault = false,
        };
        _db.ReleaseNoteTemplates.Add(template);

        await _db.SaveChangesAsync();
        return (project.Id, template.Id);
    }
}
