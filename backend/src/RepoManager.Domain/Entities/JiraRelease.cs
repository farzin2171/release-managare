namespace RepoManager.Domain.Entities;

public class JiraRelease
{
    public Guid Id { get; set; }
    public Guid JiraConnectionId { get; set; }
    public Guid ProjectId { get; set; }
    public string JiraProjectKey { get; set; } = string.Empty;
    public string JiraVersionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsReleased { get; set; }
    public DateOnly? ReleaseDate { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }

    public JiraConnection JiraConnection { get; set; } = null!;
    public ICollection<JiraTicket> JiraTickets { get; set; } = [];
}
