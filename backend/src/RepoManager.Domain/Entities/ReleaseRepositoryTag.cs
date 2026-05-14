namespace RepoManager.Domain.Entities;

public class ReleaseRepositoryTag
{
    public Guid ReleaseId { get; set; }
    public Guid RepositoryId { get; set; }
    public string FromTag { get; set; } = string.Empty;
    public string ToTag { get; set; } = string.Empty;
    public int CommitCount { get; set; }

    public Release Release { get; set; } = null!;
    public Repository Repository { get; set; } = null!;
}
