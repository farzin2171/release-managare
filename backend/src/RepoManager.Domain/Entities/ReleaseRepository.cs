namespace RepoManager.Domain.Entities;

public class ReleaseRepository
{
    public Guid Id { get; set; }
    public Guid ReleaseId { get; set; }
    public Guid RepositoryId { get; set; }
    public string PreviousVersion { get; set; } = string.Empty;
    public string NextVersion { get; set; } = string.Empty;
    public string BumpType { get; set; } = string.Empty;
    public string FromCommitSha { get; set; } = string.Empty;
    public string ToCommitSha { get; set; } = string.Empty;
    public int CommitCount { get; set; }
    public int TicketCount { get; set; }

    public Release Release { get; set; } = null!;
    public Repository Repository { get; set; } = null!;
}
