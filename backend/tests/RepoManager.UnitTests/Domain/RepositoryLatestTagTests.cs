using FluentAssertions;
using RepoManager.Domain.Entities;

namespace RepoManager.UnitTests.Domain;

public class RepositoryLatestTagTests
{
    private static Repository MakeRepository(bool isTracked) => new()
    {
        Id = Guid.NewGuid(),
        ExternalId = "ext-1",
        Name = "my-repo",
        DefaultBranch = "main",
        WebUrl = "https://example.com",
        AzureProjectName = "Project",
        IsTracked = isTracked,
    };

    // --- PinLatestTag ---

    [Fact]
    public void PinLatestTag_UntrackedRepository_ThrowsInvalidOperationException()
    {
        var repo = MakeRepository(isTracked: false);
        var userId = Guid.NewGuid();

        var act = () => repo.PinLatestTag("v1.0.0", "abc123", userId, DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*tracked*");
    }

    [Fact]
    public void PinLatestTag_TrackedRepository_SetsAllFourFields()
    {
        var repo = MakeRepository(isTracked: true);
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        repo.PinLatestTag("v2.3.0", "deadbeef01234567", userId, now);

        repo.LatestTag.Should().Be("v2.3.0");
        repo.LatestTagCommitSha.Should().Be("deadbeef01234567");
        repo.LatestTagSetAt.Should().Be(now);
        repo.LatestTagSetByUserId.Should().Be(userId);
    }

    [Fact]
    public void PinLatestTag_TrackedRepository_OverwritesPreviousPin()
    {
        var repo = MakeRepository(isTracked: true);
        var userId = Guid.NewGuid();
        repo.PinLatestTag("v1.0.0", "aaa", userId, DateTime.UtcNow.AddDays(-1));

        repo.PinLatestTag("v2.0.0", "bbb", userId, DateTime.UtcNow);

        repo.LatestTag.Should().Be("v2.0.0");
        repo.LatestTagCommitSha.Should().Be("bbb");
    }

    // --- ClearLatestTag ---

    [Fact]
    public void ClearLatestTag_WhenTagIsPinned_NullsAllFourFields()
    {
        var repo = MakeRepository(isTracked: true);
        var userId = Guid.NewGuid();
        repo.PinLatestTag("v1.0.0", "abc123", userId, DateTime.UtcNow);

        repo.ClearLatestTag(userId, DateTime.UtcNow);

        repo.LatestTag.Should().BeNull();
        repo.LatestTagCommitSha.Should().BeNull();
        repo.LatestTagSetAt.Should().BeNull();
        repo.LatestTagSetByUserId.Should().BeNull();
    }

    [Fact]
    public void ClearLatestTag_WhenNoTagPinned_SucceedsIdempotently()
    {
        var repo = MakeRepository(isTracked: true);
        var userId = Guid.NewGuid();

        var act = () => repo.ClearLatestTag(userId, DateTime.UtcNow);

        act.Should().NotThrow();
        repo.LatestTag.Should().BeNull();
    }

    [Fact]
    public void ClearLatestTag_UntrackedRepository_SucceedsIdempotently()
    {
        var repo = MakeRepository(isTracked: false);
        var userId = Guid.NewGuid();

        var act = () => repo.ClearLatestTag(userId, DateTime.UtcNow);

        act.Should().NotThrow();
    }
}
