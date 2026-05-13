namespace RepoManager.Domain.Entities;

public class ReleaseReconciliation
{
    public Guid Id { get; set; }
    public Guid ReleaseId { get; set; }
    public Guid JiraReleaseId { get; set; }
    public DateTimeOffset RunAt { get; set; }
    public int MatchedCount { get; set; }
    public int JiraOnlyCount { get; set; }
    public int GitOnlyCount { get; set; }
    public decimal MatchRatePercent { get; set; }
    public string Snapshot { get; set; } = string.Empty;

    public Release Release { get; set; } = null!;
    public JiraRelease JiraRelease { get; set; } = null!;
}
