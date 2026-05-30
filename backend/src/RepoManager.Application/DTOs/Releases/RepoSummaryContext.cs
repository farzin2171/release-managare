namespace RepoManager.Application.DTOs.Releases;

public record RepoSummaryContext(
    string Name,
    string ServiceOwner,
    string PreviousVersion,
    string NextVersion,
    int CommitCount,
    int TicketCount);
