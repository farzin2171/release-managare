namespace RepoManager.Domain.Entities;

public class Repository
{
    public Guid Id { get; set; }
    public Guid GitProviderConnectionId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = string.Empty;
    public string WebUrl { get; set; } = string.Empty;
    public string AzureProjectName { get; set; } = string.Empty;
    public bool IsTracked { get; set; } = false;
    public DateTimeOffset? LastSyncedAt { get; set; }

    public string? LatestTag { get; set; }
    public string? LatestTagCommitSha { get; set; }
    public DateTime? LatestTagSetAt { get; set; }
    public Guid? LatestTagSetByUserId { get; set; }

    public void PinLatestTag(string tagName, string commitSha, Guid userId, DateTime utcNow)
    {
        if (!IsTracked)
            throw new InvalidOperationException("Repository must be tracked to pin a latest tag.");
        LatestTag = tagName;
        LatestTagCommitSha = commitSha;
        LatestTagSetAt = utcNow;
        LatestTagSetByUserId = userId;
    }

    public void ClearLatestTag(Guid userId, DateTime utcNow)
    {
        LatestTag = null;
        LatestTagCommitSha = null;
        LatestTagSetAt = null;
        LatestTagSetByUserId = null;
    }

    public GitProviderConnection GitProviderConnection { get; set; } = null!;
    public User? LatestTagSetBy { get; set; }
    public ICollection<ProjectRepository> ProjectRepositories { get; set; } = [];
    public ICollection<Commit> Commits { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = [];
    public ICollection<ReleaseRepositoryTag> ReleaseRepositoryTags { get; set; } = [];
}
