using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.GitProviders;
using RepoManager.Application.Jira;
using RepoManager.Application.Jira.Dtos;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Commits;
using RepoManager.Infrastructure.Jira;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests.Infrastructure;

public class RepoJiraComparisonServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly Mock<IJiraService> _jiraMock;
    private readonly Mock<IGitProviderFactory> _gitFactoryMock;
    private readonly Mock<IGitProvider> _gitProviderMock;
    private readonly IDataProtectionProvider _dp;
    private readonly RepoJiraComparisonService _service;

    public RepoJiraComparisonServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(opts);
        _db.Database.EnsureCreated();

        _jiraMock = new Mock<IJiraService>();
        _gitProviderMock = new Mock<IGitProvider>();
        _gitFactoryMock = new Mock<IGitProviderFactory>();

        _gitFactoryMock
            .Setup(f => f.GetProvider(It.IsAny<ProviderType>()))
            .Returns(_gitProviderMock.Object);

        _gitProviderMock
            .Setup(p => p.GetCommitsBetweenAsync(
                It.IsAny<ProviderConnection>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<DateTimeOffset?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<CommitInfo>());

        _jiraMock
            .Setup(j => j.GetTicketsInFixVersionAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<JiraIssueSummary>());

        _jiraMock
            .Setup(j => j.AddTicketToFixVersionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _jiraMock
            .Setup(j => j.CreateFixVersionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("new-version-id");

        _dp = new EphemeralDataProtectionProvider();

        _service = new RepoJiraComparisonService(
            _db,
            _gitFactoryMock.Object,
            _jiraMock.Object,
            new ConventionalCommitParser(),
            _dp,
            NullLogger<RepoJiraComparisonService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<Repository> CreateRepoAsync(string? latestTag = "1.0.0")
    {
        var gitPatProtector = _dp.CreateProtector("GitProviderConnection.Pat");

        var conn = new GitProviderConnection
        {
            Id = Guid.NewGuid(),
            Name = "test-conn",
            ProviderType = ProviderType.AzureDevOps,
            OrganizationUrl = "https://dev.azure.com/test",
            EncryptedPat = gitPatProtector.Protect("test-pat"),
            IsActive = true
        };
        _db.GitProviderConnections.Add(conn);

        var repo = new Repository
        {
            Id = Guid.NewGuid(),
            GitProviderConnectionId = conn.Id,
            ExternalId = "ext-test-" + Guid.NewGuid(),
            Name = "test-repo",
            DefaultBranch = "main",
            WebUrl = "https://example.com",
            AzureProjectName = "TestProject",
            IsTracked = true,
            LatestTag = latestTag
        };
        _db.Repositories.Add(repo);
        await _db.SaveChangesAsync();
        return repo;
    }

    private async Task<Project> LinkRepoToProjectAsync(Repository repo, string jiraKeys = "[\"PROJ\"]")
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project " + Guid.NewGuid(),
            Color = "#3B82F6",
            JiraProjectKeys = jiraKeys,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Projects.Add(project);
        _db.ProjectRepositories.Add(new ProjectRepository
        {
            ProjectId = project.Id,
            RepositoryId = repo.Id,
            IsPrimary = true
        });
        await _db.SaveChangesAsync();
        return project;
    }

    private async Task<RepoJiraComparisonSnapshot> InsertSnapshotAsync(
        Guid repoId,
        bool fresh = true,
        bool supported = true,
        string tag = "1.0.0")
    {
        var snap = new RepoJiraComparisonSnapshot
        {
            RepositoryId = repoId,
            CurrentTag = tag,
            NextVersion = supported ? "1.1.0" : string.Empty,
            JiraFixVersionName = supported ? "test-repo_1.1.0" : string.Empty,
            JiraFixVersionExists = false,
            Supported = supported,
            UnsupportedReason = supported ? null : $"Latest tag '{tag}' is not a semver tag.",
            CommitCount = 0,
            GitTicketCount = 0,
            JiraTicketCount = 0,
            InBothCount = 0,
            JiraOnlyCount = 0,
            GitOnlyCount = 0,
            MatchRate = supported ? 1.0m : 0m,
            InBothJson = "[]",
            JiraOnlyJson = "[]",
            GitOnlyJson = "[]",
            UnmatchedCommitsJson = "[]",
            LastSyncedAt = fresh ? DateTime.UtcNow : DateTime.MinValue
        };
        _db.RepoJiraComparisonSnapshots.Add(snap);
        await _db.SaveChangesAsync();
        return snap;
    }

    // ── cache behaviour ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetForRepoAsync_FreshSnapshot_ReturnsCachedDataWithoutCallingJira()
    {
        var repo = await CreateRepoAsync("1.0.0");
        await LinkRepoToProjectAsync(repo);
        await InsertSnapshotAsync(repo.Id, fresh: true);

        var result = await _service.GetForRepoAsync(repo.Id, forceRefresh: false);

        result.RepositoryId.Should().Be(repo.Id);
        result.Supported.Should().BeTrue();
        result.MatchRate.Should().Be(1.0m);
        result.Health.Should().Be(HealthBand.Green);

        _jiraMock.Verify(
            j => j.GetTicketsInFixVersionAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "cache hit must not call Jira");
    }

    [Fact]
    public async Task GetForRepoAsync_StaleSnapshot_CallsExternalServicesAndRefreshesData()
    {
        var repo = await CreateRepoAsync("1.0.0");
        await LinkRepoToProjectAsync(repo, "[\"PROJ\"]");
        await InsertSnapshotAsync(repo.Id, fresh: false);

        var result = await _service.GetForRepoAsync(repo.Id, forceRefresh: false);

        result.Supported.Should().BeTrue();
        _jiraMock.Verify(
            j => j.GetTicketsInFixVersionAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "cache miss must call Jira");
    }

    [Fact]
    public async Task GetForRepoAsync_ForceRefresh_BypassesFreshCacheAndCallsJira()
    {
        var repo = await CreateRepoAsync("1.0.0");
        await LinkRepoToProjectAsync(repo, "[\"PROJ\"]");
        await InsertSnapshotAsync(repo.Id, fresh: true);

        var result = await _service.GetForRepoAsync(repo.Id, forceRefresh: true);

        result.Supported.Should().BeTrue();
        _jiraMock.Verify(
            j => j.GetTicketsInFixVersionAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once,
            "forceRefresh must bypass TTL and call Jira");
    }

    // ── snapshot upsert idempotency ───────────────────────────────────────────

    [Fact]
    public async Task GetForRepoAsync_CalledTwiceOnCacheMiss_UpsertsExistingRowNotDuplicates()
    {
        var repo = await CreateRepoAsync("1.0.0");
        await LinkRepoToProjectAsync(repo);

        // First call: no snapshot → compute and insert
        await _service.GetForRepoAsync(repo.Id, forceRefresh: false);

        var rowsAfterFirst = _db.RepoJiraComparisonSnapshots
            .Count(s => s.RepositoryId == repo.Id && s.Supported);
        rowsAfterFirst.Should().Be(1, "first call should create exactly one row");

        // Make the snapshot stale so the second call recomputes
        var snap = _db.RepoJiraComparisonSnapshots.Single(s => s.RepositoryId == repo.Id);
        snap.LastSyncedAt = DateTime.MinValue;
        await _db.SaveChangesAsync();

        // Second call: stale snapshot → compute and update (not insert)
        await _service.GetForRepoAsync(repo.Id, forceRefresh: false);

        var rowsAfterSecond = _db.RepoJiraComparisonSnapshots
            .Count(s => s.RepositoryId == repo.Id && s.Supported);
        rowsAfterSecond.Should().Be(1, "second call must update existing row, not create a duplicate");
    }

    [Fact]
    public async Task GetForRepoAsync_UpdatesLastSyncedAtOnCacheHit()
    {
        var repo = await CreateRepoAsync("1.0.0");
        var snap = await InsertSnapshotAsync(repo.Id, fresh: true);

        var before = repo.LastViewedAt;
        await _service.GetForRepoAsync(repo.Id, forceRefresh: false);

        await _db.Entry(repo).ReloadAsync();
        repo.LastViewedAt.Should().NotBeNull("LastViewedAt should be updated on every call");
    }

    // ── non-semver tag ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForRepoAsync_NonSemverTag_ReturnsUnsupportedDto()
    {
        var repo = await CreateRepoAsync("release-2026");

        var result = await _service.GetForRepoAsync(repo.Id, forceRefresh: false);

        result.Supported.Should().BeFalse();
        result.Health.Should().Be(HealthBand.Unknown);
        result.UnsupportedReason.Should().Contain("release-2026");

        _jiraMock.Verify(
            j => j.GetTicketsInFixVersionAsync(
                It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "non-semver repo must not call Jira");
    }

    [Fact]
    public async Task GetForRepoAsync_NoTag_ReturnsUnsupportedDtoWithNoTagReason()
    {
        var repo = await CreateRepoAsync(latestTag: null);

        var result = await _service.GetForRepoAsync(repo.Id, forceRefresh: false);

        result.Supported.Should().BeFalse();
        result.UnsupportedReason.Should().Contain("No latest tag");
    }

    [Fact]
    public async Task GetForRepoAsync_UnknownRepo_ThrowsNotFoundException()
    {
        var act = async () => await _service.GetForRepoAsync(Guid.NewGuid(), forceRefresh: false);

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Repository*");
    }

    // ── add-ticket: fix-version creation ─────────────────────────────────────

    [Fact]
    public async Task AddTicketToFixVersionAsync_CreatesFixVersionOnDemand()
    {
        var repo = await CreateRepoAsync("1.0.0");
        await InsertSnapshotAsync(repo.Id, fresh: true, supported: true);

        var result = await _service.AddTicketToFixVersionAsync(repo.Id, "PROJ-123");

        result.Success.Should().BeTrue();
        result.FixVersionCreated.Should().BeTrue();
        result.JiraFixVersionName.Should().Be("test-repo_1.1.0");

        _jiraMock.Verify(
            j => j.CreateFixVersionAsync("PROJ", "test-repo_1.1.0", It.IsAny<CancellationToken>()),
            Times.Once);
        _jiraMock.Verify(
            j => j.AddTicketToFixVersionAsync("PROJ-123", "test-repo_1.1.0", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddTicketToFixVersionAsync_FixVersionAlreadyExists_DoesNotReportCreated()
    {
        var repo = await CreateRepoAsync("1.0.0");
        await InsertSnapshotAsync(repo.Id, fresh: true, supported: true);

        _jiraMock
            .Setup(j => j.CreateFixVersionAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ExternalServiceException("Jira", "Version already exists", null));

        var result = await _service.AddTicketToFixVersionAsync(repo.Id, "PROJ-456");

        result.Success.Should().BeTrue();
        result.FixVersionCreated.Should().BeFalse();
        _jiraMock.Verify(
            j => j.AddTicketToFixVersionAsync("PROJ-456", "test-repo_1.1.0", It.IsAny<CancellationToken>()),
            Times.Once,
            "ticket should still be added even when fix version creation fails (already exists)");
    }

    // ── add-ticket: snapshot invalidation ────────────────────────────────────

    [Fact]
    public async Task AddTicketToFixVersionAsync_SetsAllSnapshotLastSyncedAtToMinValue()
    {
        var repo = await CreateRepoAsync("1.0.0");
        var snap = await InsertSnapshotAsync(repo.Id, fresh: true, supported: true);

        snap.LastSyncedAt.Should().NotBe(DateTime.MinValue);

        await _service.AddTicketToFixVersionAsync(repo.Id, "PROJ-789");

        await _db.Entry(snap).ReloadAsync();
        snap.LastSyncedAt.Should().Be(DateTime.MinValue,
            "add-ticket must invalidate all snapshots for the repo by setting LastSyncedAt = MinValue");
    }

    [Fact]
    public async Task AddTicketToFixVersionAsync_NoSupportedSnapshot_ThrowsConflictException()
    {
        var repo = await CreateRepoAsync("release-2026"); // non-semver → no supported snapshot

        var act = async () => await _service.AddTicketToFixVersionAsync(repo.Id, "PROJ-123");

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*SemVer*");
    }
}
