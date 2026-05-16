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

    public GitProviderConnection GitProviderConnection { get; set; } = null!;
    public User? LatestTagSetBy { get; set; }
    public ICollection<ProjectRepository> ProjectRepositories { get; set; } = [];
    public ICollection<Commit> Commits { get; set; } = [];
    public ICollection<Ticket> Tickets { get; set; } = [];
    public ICollection<ReleaseRepositoryTag> ReleaseRepositoryTags { get; set; } = [];
}
