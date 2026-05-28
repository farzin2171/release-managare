using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RepoManager.Application.Confluence;
using RepoManager.Application.DTOs.Releases;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;
using RepoManager.Infrastructure.Releases;
using RepoManager.Infrastructure.Services.Handlebars;

namespace RepoManager.IntegrationTests.Releases;

public class PreparePagesTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly ReleaseRenderService _service;

    public PreparePagesTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        var recorder = new MissingTokenRecorder();
        var hbs = HandlebarsFactory.Create(recorder);
        var publisherMock = new Mock<IConfluencePublisher>();

        _service = new ReleaseRenderService(
            _db,
            hbs,
            recorder,
            publisherMock.Object,
            new EphemeralDataProtectionProvider(),
            NullLogger<ReleaseRenderService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── Reconciliation rendering ──────────────────────────────────────────────

    [Fact]
    public async Task PrepareAsync_WithoutReconciliation_OmitsReconciliationBlock()
    {
        var releaseId = await SeedAsync("{{#if reconciliation}}RECON_PRESENT{{/if}}");

        var result = await _service.PrepareAsync(releaseId, new PreparePageRequest(null, null));

        result.Pages.Should().HaveCount(1);
        result.Pages[0].Body.Should().BeEmpty(
            "{{#if reconciliation}} block must not render when no reconciliation data is supplied");
    }

    [Fact]
    public async Task PrepareAsync_WithReconciliationData_RendersReconciliationBlock()
    {
        var releaseId = await SeedAsync("{{#if reconciliation}}RECON_PRESENT{{/if}}");

        var reconciliation = new ReconciliationSummaryDto(12, 3, 1, 0.8, DateTimeOffset.UtcNow);
        var result = await _service.PrepareAsync(releaseId, new PreparePageRequest(null, reconciliation));

        result.Pages.Should().HaveCount(1);
        result.Pages[0].Body.Should().Be("RECON_PRESENT",
            "{{#if reconciliation}} block must render when reconciliation data is supplied");
    }

    [Fact]
    public async Task PrepareAsync_WithReconciliationData_ExposesCountsInTemplate()
    {
        var releaseId = await SeedAsync(
            "{{#if reconciliation}}matched:{{reconciliation.matchedCount}} jiraOnly:{{reconciliation.jiraOnlyCount}}{{/if}}");

        var reconciliation = new ReconciliationSummaryDto(7, 2, 0, 1.0, DateTimeOffset.UtcNow);
        var result = await _service.PrepareAsync(releaseId, new PreparePageRequest(null, reconciliation));

        result.Pages[0].Body.Should().Be("matched:7 jiraOnly:2");
    }

    [Fact]
    public async Task PrepareAsync_ReconciliationChangedBetweenCalls_SecondCallBodyReflectsNewData()
    {
        var releaseId = await SeedAsync("{{#if reconciliation}}count:{{reconciliation.matchedCount}}{{/if}}");

        var first = await _service.PrepareAsync(releaseId,
            new PreparePageRequest(null, new ReconciliationSummaryDto(5, 0, 0, 1.0, DateTimeOffset.UtcNow)));
        var second = await _service.PrepareAsync(releaseId,
            new PreparePageRequest(null, new ReconciliationSummaryDto(10, 1, 0, 0.9, DateTimeOffset.UtcNow)));

        first.Pages[0].Body.Should().Be("count:5");
        second.Pages[0].Body.Should().Be("count:10",
            "a second prepare call with updated reconciliation data must render fresh counts");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> SeedAsync(string templateBody)
    {
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

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "TestProject",
            Color = "#000000",
            JiraProjectKeys = "[]",
            ConfluenceSpaceKey = "SPACE",
            ConfluenceParentPageId = "99999",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.Projects.Add(project);

        var template = new ReleaseNoteTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Test Template",
            ContentTemplate = templateBody,
            IsDefault = false,
        };
        _db.ReleaseNoteTemplates.Add(template);

        var binding = new ProjectTemplateBinding
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            TemplateId = template.Id,
            Kind = TemplateBindingKind.ReleaseNotes,
            PageTitleTemplate = "{{project.name}} Release Notes",
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _db.TemplateBindings.Add(binding);

        var release = new Release
        {
            Id = Guid.NewGuid(),
            ProjectId = project.Id,
            Name = "v1.0.0",
            Version = "1.0.0",
            Status = ReleaseStatus.Draft,
            GeneratedNotesMarkdown = string.Empty,
            CreatedByUserId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.Releases.Add(release);

        await _db.SaveChangesAsync();
        return release.Id;
    }
}
