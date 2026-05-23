namespace RepoManager.Application.Releases;

public interface IVersionBumpService
{
    Task<VersionBumpSuggestionDto> SuggestAsync(Guid repositoryId, CancellationToken ct = default);
}

public record VersionBumpSuggestionDto(
    string PreviousVersion,
    string SuggestedNextVersion,
    string BumpType,
    string FromCommitSha,
    string ToCommitSha,
    int CommitCount,
    int TicketCount);
