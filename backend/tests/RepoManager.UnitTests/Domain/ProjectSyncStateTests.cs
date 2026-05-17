using FluentAssertions;
using RepoManager.Domain.Aggregates;
using RepoManager.Domain.Enums;

namespace RepoManager.UnitTests.Domain;

public class ProjectSyncStateTests
{
    private static ProjectSync MakeSync(int totalRepos = 3) => new()
    {
        Id = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        Status = ProjectSyncStatus.Pending,
        StartedAt = DateTimeOffset.UtcNow,
        TotalRepos = totalRepos,
        TriggeredByUserId = Guid.NewGuid()
    };

    [Fact]
    public void Start_FromPending_TransitionsToInProgress()
    {
        var sync = MakeSync();
        sync.Start();
        sync.Status.Should().Be(ProjectSyncStatus.InProgress);
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
    public void RecordChildResult_Succeeded_IncrementsSucceededCount()
    {
        var sync = MakeSync();
        sync.Start();
        sync.RecordChildResult(SyncStatus.Succeeded);
        sync.SucceededCount.Should().Be(1);
        sync.FailedCount.Should().Be(0);
        sync.SkippedCount.Should().Be(0);
    }

    [Fact]
    public void RecordChildResult_Failed_IncrementsFailedCount()
    {
        var sync = MakeSync();
        sync.Start();
        sync.RecordChildResult(SyncStatus.Failed);
        sync.FailedCount.Should().Be(1);
    }

    [Fact]
    public void RecordChildResult_Skipped_IncrementsSkippedCount()
    {
        var sync = MakeSync();
        sync.Start();
        sync.RecordChildResult(SyncStatus.Skipped);
        sync.SkippedCount.Should().Be(1);
    }

    [Fact]
    public void RecordChildResult_WhenNotInProgress_ThrowsInvalidOperationException()
    {
        var sync = MakeSync();
        var act = () => sync.RecordChildResult(SyncStatus.Succeeded);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_AllSucceeded_TransitionsToSucceeded()
    {
        var sync = MakeSync(totalRepos: 2);
        sync.Start();
        sync.RecordChildResult(SyncStatus.Succeeded);
        sync.RecordChildResult(SyncStatus.Succeeded);
        sync.Complete();
        sync.Status.Should().Be(ProjectSyncStatus.Succeeded);
        sync.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Complete_MixedResults_TransitionsToPartiallyFailed()
    {
        var sync = MakeSync(totalRepos: 2);
        sync.Start();
        sync.RecordChildResult(SyncStatus.Succeeded);
        sync.RecordChildResult(SyncStatus.Failed);
        sync.Complete();
        sync.Status.Should().Be(ProjectSyncStatus.PartiallyFailed);
    }

    [Fact]
    public void Complete_AllFailed_TransitionsToFailed()
    {
        var sync = MakeSync(totalRepos: 2);
        sync.Start();
        sync.RecordChildResult(SyncStatus.Failed);
        sync.RecordChildResult(SyncStatus.Failed);
        sync.Complete();
        sync.Status.Should().Be(ProjectSyncStatus.Failed);
    }

    [Fact]
    public void Complete_WhenNotInProgress_ThrowsInvalidOperationException()
    {
        var sync = MakeSync();
        var act = () => sync.Complete();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancel_FromInProgress_TransitionsToCancelled()
    {
        var sync = MakeSync();
        sync.Start();
        sync.Cancel();
        sync.Status.Should().Be(ProjectSyncStatus.Cancelled);
        sync.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public void Cancel_WhenNotInProgress_ThrowsInvalidOperationException()
    {
        var sync = MakeSync();
        var act = () => sync.Cancel();
        act.Should().Throw<InvalidOperationException>();
    }
}
