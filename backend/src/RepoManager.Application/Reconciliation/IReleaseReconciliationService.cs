using RepoManager.Application.Jira;

namespace RepoManager.Application.Reconciliation;

public interface IReleaseReconciliationService
{
    Task<ReconciliationResultDto> ReconcileAsync(Guid releaseId, CancellationToken ct = default);
    Task<ReconciliationResultDto?> GetLatestAsync(Guid releaseId, CancellationToken ct = default);
    Task AddGitTicketsToJiraAsync(Guid releaseId, IReadOnlyList<string> ticketKeys, CancellationToken ct = default);
}

public record ReconciliationResultDto(
    Guid ReleaseId,
    DateTimeOffset RunAt,
    int MatchedCount,
    int JiraOnlyCount,
    int GitOnlyCount,
    decimal MatchRatePercent,
    IReadOnlyList<MatchedTicketDto> Matched,
    IReadOnlyList<JiraTicketDto> JiraOnly,
    IReadOnlyList<GitTicketDto> GitOnly);

public record MatchedTicketDto(string Key, string Summary, string Status);

public record GitTicketDto(string TicketId, string? Title, int CommitCount);
