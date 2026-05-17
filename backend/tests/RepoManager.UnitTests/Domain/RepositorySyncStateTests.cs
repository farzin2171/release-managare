using FluentAssertions;
using RepoManager.Domain.Aggregates;
using RepoManager.Domain.Enums;
using RepoManager.Domain.ValueObjects;

namespace RepoManager.UnitTests.Domain;

public class RepositorySyncStateTests
{
    private static RepositorySync MakeSync() => new()
    {
        Id = Guid.NewGuid(),
        RepositoryId = Guid.NewGuid(),
        FromTag = "v1.0.0",
        Status = SyncStatus.Pending,
        StartedAt = DateTimeOffset.UtcNow,
        TriggeredByUserId = Guid.NewGuid(),
        ContributorsJson = "[]"
    };

    [Fact]
    public void Start_FromPending_TransitionsToInProgress()
    {
        var sync = MakeSync();
        sync.Start();
        sync.Status.Should().Be(SyncStatus.InProgress);
    }

    [Fact]
    public void Start_FromNonPending_ThrowsInvalidOperationException()
    {
        var sync = MakeSync();
        sync.Start();
        var act = () => sync.Start();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Skip_FromPending_TransitionsToSkipped()
    {
        var sync = MakeSync();
        sync.Skip("NoPinnedTag");
        sync.Status.Should().Be(SyncStatus.Skipped);
        sync.SkipReason.Should().Be("NoPinnedTag");
        sync.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Skip_FromNonPending_ThrowsInvalidOperationException()
    {
        var sync = MakeSync();
        sync.Start();
        var act = () => sync.Skip("reason");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void SetStep_WhileInProgress_UpdatesCurrentStep()
    {
        var sync = MakeSync();
        sync.Start();
        sync.SetStep(SyncStep.FetchingCommits);
        sync.CurrentStep.Should().Be(SyncStep.FetchingCommits);
    }

    [Fact]
    public void SetStep_WhenNotInProgress_ThrowsInvalidOperationException()
    {
        var sync = MakeSync();
        var act = () => sync.SetStep(SyncStep.FetchingCommits);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_FromInProgress_TransitionsToSucceeded()
    {
        var sync = MakeSync();
        sync.Start();
        var contributors = new List<ContributorSnapshot> { new("Alice", "alice@example.com", 5) };

        sync.Complete(10, 3, 1, contributors);

        sync.Status.Should().Be(SyncStatus.Succeeded);
        sync.CommitCount.Should().Be(10);
        sync.TicketCount.Should().Be(3);
        sync.BreakingChangeCount.Should().Be(1);
        sync.ContributorCount.Should().Be(1);
        sync.CompletedAt.Should().NotBeNull();
        sync.CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Complete_WhenNotInProgress_ThrowsInvalidOperationException()
    {
        var sync = MakeSync();
        var act = () => sync.Complete(0, 0, 0, []);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_CalledTwice_ThrowsInvalidOperationException()
    {
        var sync = MakeSync();
        sync.Start();
        sync.Complete(1, 0, 0, []);
        var act = () => sync.Complete(1, 0, 0, []);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Fail_FromInProgress_TransitionsToFailed()
    {
        var sync = MakeSync();
        sync.Start();
        sync.Fail("something went wrong");
        sync.Status.Should().Be(SyncStatus.Failed);
        sync.ErrorMessage.Should().Be("something went wrong");
        sync.CompletedAt.Should().NotBeNull();
        sync.CurrentStep.Should().BeNull();
    }

    [Fact]
    public void Fail_WhenNotInProgress_ThrowsInvalidOperationException()
    {
        var sync = MakeSync();
        var act = () => sync.Fail("error");
        act.Should().Throw<InvalidOperationException>();
    }
}
