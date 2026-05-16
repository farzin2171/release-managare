using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;

namespace RepoManager.Domain.Aggregates;

public class ProjectSync
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public ProjectSyncStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int TotalRepos { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public Guid TriggeredByUserId { get; set; }

    public Project Project { get; set; } = null!;
    public User TriggeredBy { get; set; } = null!;
    public ICollection<RepositorySync> RepositorySyncs { get; set; } = [];

    public void Start()
    {
        if (Status != ProjectSyncStatus.Pending)
            throw new InvalidOperationException($"Cannot start a project sync in '{Status}' state.");
        Status = ProjectSyncStatus.InProgress;
    }

    public void RecordChildResult(SyncStatus childStatus)
    {
        if (Status != ProjectSyncStatus.InProgress)
            throw new InvalidOperationException($"Cannot record child result on a project sync in '{Status}' state.");
        switch (childStatus)
        {
            case SyncStatus.Succeeded: SucceededCount++; break;
            case SyncStatus.Failed:    FailedCount++;    break;
            case SyncStatus.Skipped:   SkippedCount++;   break;
        }
    }

    public void Complete()
    {
        if (Status != ProjectSyncStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete a project sync in '{Status}' state.");
        Status = ComputeFinalStatus();
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void Cancel()
    {
        if (Status != ProjectSyncStatus.InProgress)
            throw new InvalidOperationException($"Cannot cancel a project sync in '{Status}' state.");
        Status = ProjectSyncStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    private ProjectSyncStatus ComputeFinalStatus()
    {
        if (FailedCount == 0) return ProjectSyncStatus.Succeeded;
        if (SucceededCount > 0) return ProjectSyncStatus.PartiallyFailed;
        return ProjectSyncStatus.Failed;
    }
}
