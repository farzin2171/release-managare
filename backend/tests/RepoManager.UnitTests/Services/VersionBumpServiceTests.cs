using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RepoManager.Domain.Entities;
using RepoManager.Infrastructure.Persistence;
using RepoManager.Infrastructure.Releases;

namespace RepoManager.UnitTests.Services;

public class VersionBumpServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly VersionBumpService _sut;
    private readonly Guid _repoId = Guid.NewGuid();
    private readonly Guid _connectionId = Guid.NewGuid();

    public VersionBumpServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new AppDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _sut = new VersionBumpService(_db);
    }

    private void Seed(string? latestTag = null, string? latestTagCommitSha = null)
    {
        _db.ChangeTracker.Clear();

        _db.GitProviderConnections.Add(new GitProviderConnection
        {
            Id = _connectionId,
            Name = "test-conn",
            OrganizationUrl = "https://dev.azure.com/org",
            EncryptedPat = "pat",
            IsActive = true
        });
        _db.Repositories.Add(new Repository
        {
            Id = _repoId,
            GitProviderConnectionId = _connectionId,
            ExternalId = "ext-1",
            Name = "my-repo",
            DefaultBranch = "main",
            WebUrl = "https://dev.azure.com/org/my-repo",
            AzureProjectName = "project",
            IsTracked = true,
            LatestTag = latestTag,
            LatestTagCommitSha = latestTagCommitSha
        });
        _db.SaveChanges();
    }

    private void AddCommit(string sha, string type, bool isBreaking,
        string? jiraTicketId = null, DateTimeOffset? committedAt = null)
    {
        _db.Commits.Add(new Commit
        {
            Id = Guid.NewGuid(),
            RepositoryId = _repoId,
            Sha = sha,
            ShortSha = sha.Length > 7 ? sha[..7] : sha,
            Message = $"{type}: test commit",
            AuthorName = "dev",
            AuthorEmail = "dev@example.com",
            CommittedAt = committedAt ?? DateTimeOffset.UtcNow,
            Type = type,
            IsBreaking = isBreaking,
            IsConventional = true,
            JiraTicketId = jiraTicketId
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task SuggestAsync_BreakingChangeFooter_ReturnsMajorBump()
    {
        Seed(latestTag: "1.2.3", latestTagCommitSha: "aaa0000");
        AddCommit("aaa0000", "feat", false, committedAt: DateTimeOffset.UtcNow.AddHours(-2));
        AddCommit("bbb1111", "feat", true,  committedAt: DateTimeOffset.UtcNow.AddHours(-1));

        var result = await _sut.SuggestAsync(_repoId);

        result.BumpType.Should().Be("major");
        result.SuggestedNextVersion.Should().Be("2.0.0");
        result.PreviousVersion.Should().Be("1.2.3");
    }

    [Fact]
    public async Task SuggestAsync_BreakingBangSyntax_ReturnsMajorBump()
    {
        Seed(latestTag: "1.2.3", latestTagCommitSha: "aaa0000");
        AddCommit("aaa0000", "chore", false, committedAt: DateTimeOffset.UtcNow.AddHours(-2));
        AddCommit("bbb1111", "feat",  true,  committedAt: DateTimeOffset.UtcNow.AddHours(-1));

        var result = await _sut.SuggestAsync(_repoId);

        result.BumpType.Should().Be("major");
        result.SuggestedNextVersion.Should().Be("2.0.0");
    }

    [Fact]
    public async Task SuggestAsync_FeatOnly_ReturnsMinorBump()
    {
        Seed(latestTag: "1.2.3", latestTagCommitSha: "aaa0000");
        AddCommit("aaa0000", "chore", false, committedAt: DateTimeOffset.UtcNow.AddHours(-2));
        AddCommit("bbb1111", "feat",  false, committedAt: DateTimeOffset.UtcNow.AddHours(-1));

        var result = await _sut.SuggestAsync(_repoId);

        result.BumpType.Should().Be("minor");
        result.SuggestedNextVersion.Should().Be("1.3.0");
    }

    [Fact]
    public async Task SuggestAsync_FixOnly_ReturnsPatchBump()
    {
        Seed(latestTag: "1.2.3", latestTagCommitSha: "aaa0000");
        AddCommit("aaa0000", "chore", false, committedAt: DateTimeOffset.UtcNow.AddHours(-2));
        AddCommit("bbb1111", "fix",   false, committedAt: DateTimeOffset.UtcNow.AddHours(-1));

        var result = await _sut.SuggestAsync(_repoId);

        result.BumpType.Should().Be("patch");
        result.SuggestedNextVersion.Should().Be("1.2.4");
    }

    [Fact]
    public async Task SuggestAsync_NoCommitsSinceTag_ReturnsPatchWithZeroCounts()
    {
        Seed(latestTag: "2.0.0", latestTagCommitSha: "aaa0000");
        // tag commit itself — no commits after it
        AddCommit("aaa0000", "feat", false, committedAt: DateTimeOffset.UtcNow.AddHours(-1));

        var result = await _sut.SuggestAsync(_repoId);

        result.CommitCount.Should().Be(0);
        result.TicketCount.Should().Be(0);
        result.BumpType.Should().Be("patch");
        result.SuggestedNextVersion.Should().Be("2.0.1");
    }

    [Fact]
    public async Task SuggestAsync_NoSemverTag_ReturnsPreviousVersionEmptyAndDefaultVersion()
    {
        Seed(); // no tag
        AddCommit("ccc2222", "feat", false, committedAt: DateTimeOffset.UtcNow.AddHours(-1));

        var result = await _sut.SuggestAsync(_repoId);

        result.PreviousVersion.Should().Be(string.Empty);
        result.FromCommitSha.Should().Be(string.Empty);
        result.SuggestedNextVersion.Should().Be("0.1.0");
        result.BumpType.Should().Be("minor");
    }

    [Fact]
    public async Task SuggestAsync_CountsUniqueJiraTickets()
    {
        Seed(latestTag: "1.0.0", latestTagCommitSha: "aaa0000");
        var t0 = DateTimeOffset.UtcNow;
        AddCommit("aaa0000", "chore", false, committedAt: t0.AddHours(-3));
        AddCommit("bbb1111", "fix", false, "PROJ-1",  t0.AddHours(-2));
        AddCommit("ccc2222", "fix", false, "PROJ-1",  t0.AddHours(-1)); // duplicate
        AddCommit("ddd3333", "fix", false, "PROJ-2",  t0);

        var result = await _sut.SuggestAsync(_repoId);

        result.TicketCount.Should().Be(2);
        result.CommitCount.Should().Be(3);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
