using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Confluence;
using RepoManager.Application.DTOs.Bindings;
using RepoManager.Application.DTOs.Releases;
using RepoManager.Application.Validators;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;
using RepoManager.Infrastructure.Releases;
using RepoManager.Infrastructure.Services.Handlebars;
using RepoManager.Infrastructure.Templates;

namespace RepoManager.IntegrationTests.Releases;

public class PublishPagesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly Mock<IConfluencePublisher> _publisherMock;
    private readonly ReleaseRenderService _renderService;
    private readonly IDataProtectionProvider _dataProtection;

    public PublishPagesTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        // Use a real EphemeralDataProtectionProvider — same instance for seeding and the service
        _dataProtection = new EphemeralDataProtectionProvider();

        _publisherMock = new Mock<IConfluencePublisher>();
        SetupPublisherSuccess();

        var hbs = HandlebarsFactory.Create(new MissingTokenRecorder());

        _renderService = new ReleaseRenderService(
            _db,
            hbs,
            new MissingTokenRecorder(),
            _publisherMock.Object,
            _dataProtection,
            NullLogger<ReleaseRenderService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── Publish pages ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PublishAsync_ValidPage_CallsPublisher()
    {
        var releaseId = await SeedReleaseAsync();

        var result = await _renderService.PublishAsync(releaseId, new PublishPagesRequest([
            new(Guid.NewGuid(), "Release Notes Page", "## Notes", null, false, 0),
        ]));

        result.PublishedPages.Should().HaveCount(1);
        result.PublishedPages[0].Title.Should().Be("Release Notes Page");
        result.PublishedPages[0].ConfluencePageId.Should().NotBeNullOrEmpty();

        _publisherMock.Verify(p =>
            p.CreateOrUpdatePageAsync(
                It.IsAny<ConfluenceConnectionDto>(),
                It.IsAny<string>(), It.IsAny<string>(),
                "Release Notes Page",
                It.IsAny<string>(), null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_IdempotentRePublish_PassesExistingPageId()
    {
        var releaseId = await SeedReleaseAsync();
        const string existingId = "page-abc-123";

        await _renderService.PublishAsync(releaseId, new PublishPagesRequest([
            new(Guid.NewGuid(), "Release Notes", "## Notes", null, false, 0, existingId),
        ]));

        // Publisher must be called with the existing page ID so Confluence updates rather than creates
        _publisherMock.Verify(p =>
            p.CreateOrUpdatePageAsync(
                It.IsAny<ConfluenceConnectionDto>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                existingId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_EmptyTitle_ThrowsValidationException()
    {
        var releaseId = await SeedReleaseAsync();

        var act = () => _renderService.PublishAsync(releaseId, new PublishPagesRequest([
            new(Guid.NewGuid(), "", "## Body", null, false, 0),
        ]));

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Failures.Any(f => f.ErrorCode == "invalid_page_title"));
    }

    [Fact]
    public async Task PublishAsync_TitleExceeds255Chars_ThrowsValidationException()
    {
        var releaseId = await SeedReleaseAsync();

        var act = () => _renderService.PublishAsync(releaseId, new PublishPagesRequest([
            new(Guid.NewGuid(), new string('A', 256), "Body", null, false, 0),
        ]));

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Failures.Any(f => f.ErrorCode == "invalid_page_title"));
    }

    [Fact]
    public async Task PublishAsync_PublisherFails_ThrowsExternalServiceException()
    {
        var releaseId = await SeedReleaseAsync();

        _publisherMock
            .Setup(p => p.CreateOrUpdatePageAsync(
                It.IsAny<ConfluenceConnectionDto>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PublishResult(false, null, null, "Confluence returned 500"));

        var act = () => _renderService.PublishAsync(releaseId, new PublishPagesRequest([
            new(Guid.NewGuid(), "Title", "Body", null, false, 0),
        ]));

        await act.Should().ThrowAsync<ExternalServiceException>()
            .Where(e => e.Service == "Confluence");
    }

    [Fact]
    public async Task PublishAsync_MultiplePages_PublishesInSortOrder()
    {
        var releaseId = await SeedReleaseAsync();
        var callOrder = new List<string>();

        _publisherMock
            .Setup(p => p.CreateOrUpdatePageAsync(
                It.IsAny<ConfluenceConnectionDto>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((ConfluenceConnectionDto _, string _, string _, string title, string _, string? _, CancellationToken _) =>
                callOrder.Add(title))
            .ReturnsAsync(new PublishResult(true, Guid.NewGuid().ToString("N"), "https://c.example.com/p", null));

        // Intentionally pass out of sort order — service must reorder
        await _renderService.PublishAsync(releaseId, new PublishPagesRequest([
            new(Guid.NewGuid(), "Checklist", "- item", null, true, 1),
            new(Guid.NewGuid(), "Release Notes", "## Notes", null, false, 0),
        ]));

        callOrder[0].Should().Be("Release Notes");
        callOrder[1].Should().Be("Checklist");
    }

    [Fact]
    public async Task PublishAsync_WithCrossLinks_UpdatesPrimaryPageAfterPublish()
    {
        var releaseId = await SeedReleaseAsync();
        var publishCallTitles = new List<string>();

        _publisherMock
            .Setup(p => p.CreateOrUpdatePageAsync(
                It.IsAny<ConfluenceConnectionDto>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((ConfluenceConnectionDto _, string _, string _, string title, string _, string? _, CancellationToken _) =>
                publishCallTitles.Add(title))
            .ReturnsAsync(new PublishResult(true, Guid.NewGuid().ToString("N"), "https://c.example.com/p", null));

        var template = new ReleaseNoteTemplate { Id = Guid.NewGuid(), Name = "T", ContentTemplate = "", IsDefault = false };
        _db.ReleaseNoteTemplates.Add(template);
        var project = await _db.Releases.Where(r => r.Id == releaseId)
            .Select(r => r.Project).FirstAsync();
        var binding = new ProjectTemplateBinding
        {
            Id = Guid.NewGuid(), ProjectId = project.Id, TemplateId = template.Id,
            Kind = TemplateBindingKind.ReleaseNotes, PageTitleTemplate = "RN",
            SortOrder = 0, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.TemplateBindings.Add(binding);
        await _db.SaveChangesAsync();

        await _renderService.PublishAsync(releaseId, new PublishPagesRequest([
            new(binding.Id, "Release Notes", "## Notes", null, false, 0),
            new(Guid.NewGuid(), "Checklist", "- item", null, true, 1), // LinkFromReleaseNotes = true
        ]));

        // Primary page is published once (initial) plus once more (cross-link update) = 2 calls with "Release Notes"
        publishCallTitles.Where(t => t == "Release Notes").Should().HaveCount(2,
            "primary release notes page is updated a second time to append cross-links");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetupPublisherSuccess()
    {
        _publisherMock
            .Setup(p => p.CreateOrUpdatePageAsync(
                It.IsAny<ConfluenceConnectionDto>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfluenceConnectionDto _, string _, string _, string title, string _, string? existing, CancellationToken _) =>
                new PublishResult(
                    true,
                    existing ?? Guid.NewGuid().ToString("N"),
                    $"https://confluence.example.com/pages/{Math.Abs(title.GetHashCode()):X}",
                    null));
    }

    private async Task<Guid> SeedReleaseAsync()
    {
        var protector = _dataProtection.CreateProtector("ConfluenceConnection.ApiToken");

        var confluenceConn = new ConfluenceConnection
        {
            Id = Guid.NewGuid(),
            BaseUrl = "https://example.atlassian.net",
            Username = "user@example.com",
            EncryptedApiToken = protector.Protect("my-api-token"),
            IsActive = true,
        };
        _db.ConfluenceConnections.Add(confluenceConn);

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = $"Proj-{Guid.NewGuid():N}",
            Color = "#3B82F6",
            JiraProjectKeys = "[]",
            ConfluenceSpaceKey = "SPACE",
            ConfluenceParentPageId = "12345",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Projects.Add(project);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"u{Guid.NewGuid():N}@t.com",
            PasswordHash = "h",
            Role = Role.Admin,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Users.Add(user);

        var release = new Release
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "v1.0.0",
            Version = "1.0.0",
            Status = ReleaseStatus.Draft,
            GeneratedNotesMarkdown = "",
            CreatedByUserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Releases.Add(release);

        await _db.SaveChangesAsync();
        return release.Id;
    }
}
