namespace RepoManager.Application.DTOs.Releases;

public record ReleaseRenderContextDto(
    ProjectInfoDto Project,
    string Version,
    string PreviousVersion,
    DateTimeOffset ReleaseDate,
    IReadOnlyList<RepoContextDto> Repositories,
    TicketBucketsDto Tickets,
    IReadOnlyList<ContributorDto> Contributors,
    ReconciliationSummaryDto? Reconciliation,
    ConfluenceTargetDto Confluence,
    IReadOnlyDictionary<string, string> Custom);

public record ProjectInfoDto(Guid Id, string Name, string? Description);

public record RepoContextDto(
    string Name,
    string PreviousTag,
    string NextTag,
    int CommitCount,
    int TicketCount,
    string JiraFixVersion);

public record TicketBucketsDto(
    IReadOnlyList<TicketDto> Breaking,
    IReadOnlyList<TicketDto> Features,
    IReadOnlyList<TicketDto> Fixes,
    IReadOnlyList<TicketDto> Other);

public record TicketDto(
    string Id,
    string Summary,
    string? Type,
    bool IsBreaking);

public record ContributorDto(string Name, string Email, int CommitCount);

public record ReconciliationSummaryDto(
    int MatchedCount,
    int JiraOnlyCount,
    int GitOnlyCount,
    double MatchRate,
    DateTimeOffset RunAt);

public record ConfluenceTargetDto(string SpaceKey, string ParentPageId);
