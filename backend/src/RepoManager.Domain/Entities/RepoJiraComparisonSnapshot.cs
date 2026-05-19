namespace RepoManager.Domain.Entities;

public class RepoJiraComparisonSnapshot
{
    public int Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string CurrentTag { get; set; } = string.Empty;
    public string NextVersion { get; set; } = string.Empty;
    public string JiraFixVersionName { get; set; } = string.Empty;
    public bool JiraFixVersionExists { get; set; }
    public int CommitCount { get; set; }
    public int GitTicketCount { get; set; }
    public int JiraTicketCount { get; set; }
    public int InBothCount { get; set; }
    public int JiraOnlyCount { get; set; }
    public int GitOnlyCount { get; set; }
    public decimal MatchRate { get; set; }
    public bool Supported { get; set; }
    public string? UnsupportedReason { get; set; }
    public string InBothJson { get; set; } = "[]";
    public string JiraOnlyJson { get; set; } = "[]";
    public string GitOnlyJson { get; set; } = "[]";
    public string UnmatchedCommitsJson { get; set; } = "[]";
    public DateTime LastSyncedAt { get; set; }
    public string? LastSyncError { get; set; }

    public Repository Repository { get; set; } = null!;
}
