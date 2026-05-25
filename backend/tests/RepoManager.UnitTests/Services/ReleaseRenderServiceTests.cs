using FluentAssertions;
using HandlebarsDotNet;
using Microsoft.EntityFrameworkCore;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.DTOs.Releases;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;
using RepoManager.Infrastructure.Releases;
using RepoManager.Infrastructure.Services.Handlebars;
using ValidationException = RepoManager.Application.Common.Exceptions.ValidationException;

namespace RepoManager.UnitTests.Services;

[Trait("Category", "Unit")]
public class ReleaseRenderServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly MissingTokenRecorder _recorder;
    private readonly IHandlebars _hbs;
    private readonly ReleaseRenderService _sut;

    private readonly Guid _connId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public ReleaseRenderServiceTests()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new AppDbContext(opts);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _recorder = new MissingTokenRecorder();
        _hbs = HandlebarsFactory.Create(_recorder);
        _sut = new ReleaseRenderService(_db, _hbs, _recorder);

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
        _db.SaveChanges();
    }

    private (Guid projectId, Guid releaseId, Guid repoId) SeedProjectRelease(
        string releaseVersion = "1.0.0",
        string? snapshotNextVersion = null,
        VersionBumpStrategy bumpStrategy = VersionBumpStrategy.Minor)
    {
        _db.ChangeTracker.Clear();

        var repoId = Guid.NewGuid();
        _db.Repositories.Add(new Repository
        {
            Id = repoId,
            GitProviderConnectionId = _connId,
            ExternalId = $"ext-{repoId}",
            Name = "my-repo",
            DefaultBranch = "main",
            WebUrl = "https://dev.azure.com/r",
            AzureProjectName = "proj",
            LatestTag = string.IsNullOrEmpty(releaseVersion) ? null : releaseVersion
        });

        var projectId = Guid.NewGuid();
        _db.Projects.Add(new Project
        {
            Id = projectId,
            Name = $"Project-{projectId}",
            VersionBumpStrategy = bumpStrategy,
            ConfluenceSpaceKey = "TS",
            ConfluenceParentPageId = "12345",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _db.ProjectRepositories.Add(new ProjectRepository
        {
            ProjectId = projectId,
            RepositoryId = repoId,
            IsPrimary = true
        });

        var releaseId = Guid.NewGuid();
        _db.Releases.Add(new Release
        {
            Id = releaseId,
            ProjectId = projectId,
            Version = releaseVersion,
            Status = ReleaseStatus.Draft,
            GeneratedNotesMarkdown = string.Empty,
            CreatedByUserId = _userId,
            CreatedAt = DateTimeOffset.UtcNow
        });

        if (snapshotNextVersion is not null)
        {
            _db.ReleaseRepositories.Add(new ReleaseRepository
            {
                Id = Guid.NewGuid(),
                ReleaseId = releaseId,
                RepositoryId = repoId,
                PreviousVersion = "0.9.0",
                NextVersion = snapshotNextVersion,
                BumpType = "minor",
                FromCommitSha = "abc",
                ToCommitSha = "def",
                CommitCount = 5,
                TicketCount = 2
            });
        }

        _db.SaveChanges();
        return (projectId, releaseId, repoId);
    }

    private void SeedReleaseNotesBinding(Guid projectId, string templateContent = "test body")
    {
        var templateId = Guid.NewGuid();
        _db.ReleaseNoteTemplates.Add(new ReleaseNoteTemplate
        {
            Id = templateId,
            Name = $"Tpl-{templateId}",
            ContentTemplate = templateContent
        });
        _db.TemplateBindings.Add(new ProjectTemplateBinding
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            TemplateId = templateId,
            Kind = TemplateBindingKind.ReleaseNotes,
            PageTitleTemplate = "Release Notes",
            SortOrder = 0,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task RenderContext_WithUnknownToken_RendersEmptyString()
    {
        var (projectId, releaseId, _) = SeedProjectRelease(snapshotNextVersion: "1.0.0");
        SeedReleaseNotesBinding(projectId, templateContent: "Hello {{custom.missingKey}}");

        var result = await _sut.PrepareAsync(releaseId, new PreparePageRequest(null, null));

        result.Pages[0].Body.Should().Be("Hello ");
    }

    [Fact]
    public async Task RenderContext_WithUnknownToken_CapturesTokenName()
    {
        var (projectId, releaseId, _) = SeedProjectRelease(snapshotNextVersion: "1.0.0");
        SeedReleaseNotesBinding(projectId, templateContent: "{{custom.missingKey}}");

        var result = await _sut.PrepareAsync(releaseId, new PreparePageRequest(null, null));

        result.Pages[0].UnknownTokens.Should().Contain("custom.missingKey");
    }

    [Fact]
    public async Task PreparePages_WhenNoSemverTag_ThrowsValidationException()
    {
        // Release with empty version and no snapshot → no version can be resolved
        var (projectId, releaseId, _) = SeedProjectRelease(releaseVersion: "");
        SeedReleaseNotesBinding(projectId);

        var act = () => _sut.PrepareAsync(releaseId, new PreparePageRequest(null, null));

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Failures.Any(f => f.ErrorCode == "no_semver_tag"));
    }

    [Fact]
    public async Task PreparePages_WhenNoReleaseNotesBinding_ThrowsValidationException()
    {
        var (_, releaseId, _) = SeedProjectRelease(snapshotNextVersion: "1.0.0");
        // No bindings added

        var act = () => _sut.PrepareAsync(releaseId, new PreparePageRequest(null, null));

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Failures.Any(f => f.ErrorCode == "no_release_notes_binding"));
    }

    [Fact]
    public async Task BuildContext_PrimaryRepoHasReleaseRepositoryRow_UsesSnapshotVersion()
    {
        // Release.Version = "1.0.0" but primary repo snapshot says "2.3.4"
        var (projectId, releaseId, _) = SeedProjectRelease(
            releaseVersion: "1.0.0",
            snapshotNextVersion: "2.3.4");
        SeedReleaseNotesBinding(projectId);

        var result = await _sut.PrepareAsync(releaseId, new PreparePageRequest(null, null));

        result.Context.Version.Should().Be("2.3.4");
    }

    [Fact]
    public async Task BuildContext_NoPrimaryRepoSnapshot_AppliesBumpStrategy()
    {
        // No ReleaseRepository row; Release.Version = "1.0.1" already computed via bump
        var (projectId, releaseId, _) = SeedProjectRelease(
            releaseVersion: "1.0.1",
            snapshotNextVersion: null,
            bumpStrategy: VersionBumpStrategy.Patch);
        SeedReleaseNotesBinding(projectId);

        var result = await _sut.PrepareAsync(releaseId, new PreparePageRequest(null, null));

        result.Context.Version.Should().Be("1.0.1");
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
