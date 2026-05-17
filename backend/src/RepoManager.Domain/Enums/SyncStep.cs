namespace RepoManager.Domain.Enums;

public static class SyncStep
{
    public const string FetchingCommits = "FetchingCommits";
    public const string ParsingCommits = "ParsingCommits";
    public const string PersistingCommits = "PersistingCommits";
    public const string AggregatingTickets = "AggregatingTickets";
    public const string Finalising = "Finalising";
}
