namespace RepoManager.Domain.Enums;

public enum ProjectSyncStatus
{
    Pending = 0,
    InProgress = 1,
    Succeeded = 2,
    PartiallyFailed = 3,
    Failed = 4,
    Cancelled = 5
}
