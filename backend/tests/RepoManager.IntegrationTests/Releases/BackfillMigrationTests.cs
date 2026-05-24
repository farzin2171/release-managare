using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests.Releases;

/// <summary>
/// Verifies the AddReleaseRepository migration backfill SQL:
/// - Creates one ReleaseRepository row per existing Release (using its project's primary repo).
/// - Is idempotent: running the backfill twice produces no duplicates.
/// </summary>
public class BackfillMigrationTests : IDisposable
{
    // The backfill SQL from the AddReleaseRepository migration
    private const string BackfillSql = @"
        INSERT OR IGNORE INTO ReleaseRepositories
            (Id, ReleaseId, RepositoryId, PreviousVersion, NextVersion, BumpType,
             FromCommitSha, ToCommitSha, CommitCount, TicketCount)
        SELECT
            lower(hex(randomblob(4))) || '-' ||
            lower(hex(randomblob(2))) || '-4' ||
            substr(lower(hex(randomblob(2))), 2) || '-' ||
            substr('89ab', abs(random()) % 4 + 1, 1) ||
            substr(lower(hex(randomblob(2))), 2) || '-' ||
            lower(hex(randomblob(6))),
            r.Id,
            pr.RepositoryId,
            '',
            r.Version,
            'manual',
            '', '', 0, 0
        FROM Releases r
        JOIN ProjectRepositories pr ON pr.ProjectId = r.ProjectId AND pr.IsPrimary = 1
        WHERE NOT EXISTS (
            SELECT 1 FROM ReleaseRepositories rr
            WHERE rr.ReleaseId = r.Id AND rr.RepositoryId = pr.RepositoryId
        )";

    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public BackfillMigrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Backfill_CreatesLegacyRow_ForEachReleaseWithPrimaryRepo()
    {
        // Seed pre-migration state: a project with a primary repo and an existing release
        var (releaseId, primaryRepoId) = await SeedReleaseWithPrimaryRepoAsync("1.0.0");

        // Act — run the backfill SQL (simulates migration Up())
        await _db.Database.ExecuteSqlRawAsync(BackfillSql);

        // Assert: one legacy row created
        var rows = await _db.ReleaseRepositories.ToListAsync();
        rows.Should().HaveCount(1);

        var row = rows[0];
        row.ReleaseId.Should().Be(releaseId);
        row.RepositoryId.Should().Be(primaryRepoId);
        row.BumpType.Should().Be("manual");
        row.NextVersion.Should().Be("1.0.0");
        row.PreviousVersion.Should().BeEmpty();
        row.FromCommitSha.Should().BeEmpty();
        row.ToCommitSha.Should().BeEmpty();
        row.CommitCount.Should().Be(0);
        row.TicketCount.Should().Be(0);
    }

    [Fact]
    public async Task Backfill_IsIdempotent_RunningTwiceProducesNoDuplicates()
    {
        await SeedReleaseWithPrimaryRepoAsync("2.0.0");

        // First run
        await _db.Database.ExecuteSqlRawAsync(BackfillSql);
        var afterFirst = await _db.ReleaseRepositories.CountAsync();

        // Second run — must not produce duplicates
        await _db.Database.ExecuteSqlRawAsync(BackfillSql);
        var afterSecond = await _db.ReleaseRepositories.CountAsync();

        afterSecond.Should().Be(afterFirst, "running backfill twice must not produce duplicates");
        afterFirst.Should().Be(1);
    }

    [Fact]
    public async Task Backfill_SkipsReleases_WhenProjectHasNoPrimaryRepo()
    {
        // Seed a release whose project has NO primary repo
        await SeedReleaseWithoutPrimaryRepoAsync();

        await _db.Database.ExecuteSqlRawAsync(BackfillSql);

        var count = await _db.ReleaseRepositories.CountAsync();
        count.Should().Be(0, "releases without a primary repo should produce no backfill rows");
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task<(Guid releaseId, Guid repoId)> SeedReleaseWithPrimaryRepoAsync(string version)
    {
        var conn = new Domain.Entities.GitProviderConnection
        {
            Id = Guid.NewGuid(), Name = "conn", ProviderType = Domain.Enums.ProviderType.AzureDevOps,
            OrganizationUrl = "https://dev.azure.com/x", EncryptedPat = "enc", IsActive = true
        };
        _db.GitProviderConnections.Add(conn);

        var repo = new Domain.Entities.Repository
        {
            Id = Guid.NewGuid(), ExternalId = $"ext-{Guid.NewGuid():N}", Name = "Repo",
            DefaultBranch = "main", WebUrl = "https://example.com",
            AzureProjectName = "proj", GitProviderConnectionId = conn.Id
        };
        _db.Repositories.Add(repo);

        var project = new Domain.Entities.Project
        {
            Id = Guid.NewGuid(), Name = $"Proj-{Guid.NewGuid():N}", Color = "#000",
            JiraProjectKeys = "[]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Projects.Add(project);

        _db.ProjectRepositories.Add(new Domain.Entities.ProjectRepository
        {
            ProjectId = project.Id, RepositoryId = repo.Id, IsPrimary = true
        });

        var user = new Domain.Entities.User
        {
            Id = Guid.NewGuid(), Email = $"u{Guid.NewGuid():N}@t.com",
            PasswordHash = "h", Role = Domain.Enums.Role.Admin,
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(user);

        var release = new Domain.Entities.Release
        {
            Id = Guid.NewGuid(), ProjectId = project.Id, Name = "R", Version = version,
            Status = Domain.Enums.ReleaseStatus.Draft, GeneratedNotesMarkdown = "",
            CreatedByUserId = user.Id, CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Releases.Add(release);

        await _db.SaveChangesAsync();
        return (release.Id, repo.Id);
    }

    private async Task SeedReleaseWithoutPrimaryRepoAsync()
    {
        var conn = new Domain.Entities.GitProviderConnection
        {
            Id = Guid.NewGuid(), Name = "conn2", ProviderType = Domain.Enums.ProviderType.AzureDevOps,
            OrganizationUrl = "https://dev.azure.com/x2", EncryptedPat = "enc2", IsActive = true
        };
        _db.GitProviderConnections.Add(conn);

        var repo = new Domain.Entities.Repository
        {
            Id = Guid.NewGuid(), ExternalId = $"ext-{Guid.NewGuid():N}", Name = "Repo2",
            DefaultBranch = "main", WebUrl = "https://example.com/2",
            AzureProjectName = "proj2", GitProviderConnectionId = conn.Id
        };
        _db.Repositories.Add(repo);

        var project = new Domain.Entities.Project
        {
            Id = Guid.NewGuid(), Name = $"ProjNoPrimary-{Guid.NewGuid():N}", Color = "#000",
            JiraProjectKeys = "[]", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.Projects.Add(project);

        // Non-primary link
        _db.ProjectRepositories.Add(new Domain.Entities.ProjectRepository
        {
            ProjectId = project.Id, RepositoryId = repo.Id, IsPrimary = false
        });

        var user = new Domain.Entities.User
        {
            Id = Guid.NewGuid(), Email = $"u2{Guid.NewGuid():N}@t.com",
            PasswordHash = "h", Role = Domain.Enums.Role.Admin,
            IsActive = true, CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(user);

        var release = new Domain.Entities.Release
        {
            Id = Guid.NewGuid(), ProjectId = project.Id, Name = "R2", Version = "0.0.1",
            Status = Domain.Enums.ReleaseStatus.Draft, GeneratedNotesMarkdown = "",
            CreatedByUserId = user.Id, CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Releases.Add(release);

        await _db.SaveChangesAsync();
    }
}
