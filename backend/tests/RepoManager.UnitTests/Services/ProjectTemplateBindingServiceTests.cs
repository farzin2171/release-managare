using FluentAssertions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.DTOs.Bindings;
using RepoManager.Application.Validators;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;
using RepoManager.Infrastructure.Templates;
using ConflictException = RepoManager.Application.Common.Exceptions.ConflictException;

namespace RepoManager.UnitTests.Services;

[Trait("Category", "Unit")]
public class ProjectTemplateBindingServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly ProjectTemplateBindingService _sut;

    private readonly Guid _connId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _templateId = Guid.NewGuid();

    public ProjectTemplateBindingServiceTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new AppDbContext(opts);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new ProjectTemplateBindingService(
            _db,
            new CreateBindingRequestValidator(),
            new UpdateBindingRequestValidator());

        SeedBaseEntities();
    }

    private void SeedBaseEntities()
    {
        _db.GitProviderConnections.Add(new GitProviderConnection
        {
            Id = _connId,
            Name = "conn",
            OrganizationUrl = "https://dev.azure.com/org",
            EncryptedPat = "pat",
            IsActive = true
        });
        _db.Users.Add(new User
        {
            Id = _userId,
            Email = "test@test.com",
            PasswordHash = "hash",
            Role = Role.Admin,
            CreatedAt = DateTimeOffset.UtcNow
        });
        _db.Projects.Add(new Project
        {
            Id = _projectId,
            Name = "Test Project",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _db.ReleaseNoteTemplates.Add(new ReleaseNoteTemplate
        {
            Id = _templateId,
            Name = "Template A",
            ContentTemplate = "body"
        });
        _db.SaveChanges();
    }

    private Guid SeedBinding(TemplateBindingKind kind, int sortOrder = 0)
    {
        var id = Guid.NewGuid();
        _db.TemplateBindings.Add(new ProjectTemplateBinding
        {
            Id = id,
            ProjectId = _projectId,
            TemplateId = _templateId,
            Kind = kind,
            PageTitleTemplate = "Release Notes",
            SortOrder = sortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();
        return id;
    }

    [Fact]
    public async Task Delete_LastReleaseNotesBinding_ThrowsConflictException()
    {
        var bindingId = SeedBinding(TemplateBindingKind.ReleaseNotes);

        var act = () => _sut.DeleteAsync(_projectId, bindingId);

        await act.Should().ThrowAsync<ConflictException>()
            .Where(e => e.Code == "last_release_notes_binding");
    }

    [Fact]
    public async Task Create_SecondReleaseNotesBinding_ThrowsConflictException()
    {
        SeedBinding(TemplateBindingKind.ReleaseNotes);

        var request = new CreateBindingRequest(
            _templateId, "ReleaseNotes", "Second Release Notes",
            null, false, 1);

        var act = () => _sut.CreateAsync(_projectId, request);

        await act.Should().ThrowAsync<ConflictException>()
            .Where(e => e.Code == "duplicate_release_notes_binding");
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
