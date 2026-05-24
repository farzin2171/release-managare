using RepoManager.Domain.Enums;

namespace RepoManager.Domain.Entities;

public class Release
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public ReleaseStatus Status { get; set; } = ReleaseStatus.Draft;
    public string GeneratedNotesMarkdown { get; set; } = string.Empty;
    public string? EditedNotesMarkdown { get; set; }
    public string? ConfluencePageId { get; set; }
    public string? ConfluencePageUrl { get; set; }
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    // Pessimistic edit lock (10-minute TTL, refreshed on each PUT)
    public Guid? EditLockedByUserId { get; set; }
    public DateTimeOffset? EditLockExpiresAt { get; set; }
    public string? EditLockedByUserName { get; set; }

    public Project Project { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public ICollection<ReleaseRepositoryTag> RepositoryTags { get; set; } = [];
    public ICollection<ReleaseRepository> ReleaseRepositories { get; set; } = [];
    public ReleaseReconciliation? Reconciliation { get; set; }
}
