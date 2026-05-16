using System.Text.Json;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Domain.ValueObjects;

namespace RepoManager.Domain.Aggregates;

public class RepositorySync
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid? ProjectSyncId { get; set; }
    public string FromTag { get; set; } = string.Empty;
    public string? ToCommitSha { get; set; }
    public SyncStatus Status { get; set; }
    public string? SkipReason { get; set; }
    public string? CurrentStep { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int CommitCount { get; set; }
    public int TicketCount { get; set; }
    public int ContributorCount { get; set; }
    public int BreakingChangeCount { get; set; }
    public string ContributorsJson { get; set; } = "[]";
    public string? ErrorMessage { get; set; }
    public Guid TriggeredByUserId { get; set; }

    public Repository Repository { get; set; } = null!;
    public ProjectSync? ProjectSync { get; set; }
    public User TriggeredBy { get; set; } = null!;

    public void Start()
    {
        if (Status != SyncStatus.Pending)
            throw new InvalidOperationException($"Cannot start a sync in '{Status}' state.");
        Status = SyncStatus.InProgress;
    }

    public void Skip(string reason)
    {
        if (Status != SyncStatus.Pending)
            throw new InvalidOperationException($"Cannot skip a sync in '{Status}' state.");
        Status = SyncStatus.Skipped;
        SkipReason = reason;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    public void SetStep(string step)
    {
        if (Status != SyncStatus.InProgress)
            throw new InvalidOperationException($"Cannot set step on a sync in '{Status}' state.");
        CurrentStep = step;
    }

    public void Complete(int commitCount, int ticketCount, int breakingCount, IList<ContributorSnapshot> contributors)
    {
        if (Status != SyncStatus.InProgress)
            throw new InvalidOperationException($"Cannot complete a sync in '{Status}' state.");
        CommitCount = commitCount;
        TicketCount = ticketCount;
        BreakingChangeCount = breakingCount;
        ContributorCount = contributors.Count;
        ContributorsJson = JsonSerializer.Serialize(contributors);
        Status = SyncStatus.Succeeded;
        CompletedAt = DateTimeOffset.UtcNow;
        CurrentStep = null;
    }

    public void Fail(string message)
    {
        if (Status != SyncStatus.InProgress)
            throw new InvalidOperationException($"Cannot fail a sync in '{Status}' state.");
        ErrorMessage = message;
        Status = SyncStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
        CurrentStep = null;
    }
}
