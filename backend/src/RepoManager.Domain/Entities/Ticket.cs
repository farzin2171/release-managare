namespace RepoManager.Domain.Entities;

public class Ticket
{
    public Guid Id { get; set; }
    public string TicketId { get; set; } = string.Empty;
    public Guid RepositoryId { get; set; }
    public string FromTag { get; set; } = string.Empty;
    public string ToTag { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? PrimaryType { get; set; }
    public bool IsBreaking { get; set; }
    public int CommitCount { get; set; }
    public int ContributorCount { get; set; }
    public DateTimeOffset FirstCommittedAt { get; set; }
    public DateTimeOffset LastCommittedAt { get; set; }

    public Repository Repository { get; set; } = null!;
}
