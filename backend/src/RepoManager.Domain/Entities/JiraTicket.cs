using RepoManager.Domain.Enums;

namespace RepoManager.Domain.Entities;

public class JiraTicket
{
    public Guid Id { get; set; }
    public Guid JiraReleaseId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public JiraStatusCategory StatusCategory { get; set; }
    public string IssueType { get; set; } = string.Empty;
    public string? AssigneeName { get; set; }
    public string? AssigneeEmail { get; set; }
    public string? Priority { get; set; }
    public string? ParentKey { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }

    public JiraRelease JiraRelease { get; set; } = null!;
}
