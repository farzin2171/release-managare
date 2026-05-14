using System.Text.RegularExpressions;
using RepoManager.Application.Commits;

namespace RepoManager.Infrastructure.Commits;

public class ConventionalCommitParser : IConventionalCommitParser
{
    // Spec regex — type captured as \w+ then validated lowercase below
    private static readonly Regex HeaderRegex = new(
        @"^(?<type>\w+)(\((?<scope>[^)]+)\))?(?<breaking>!)?:\s*(?<desc>.+)$",
        RegexOptions.Compiled);

    // Jira ticket key: 2–10 uppercase letters, dash, one or more digits
    private static readonly Regex JiraPattern = new(
        @"^[A-Z]{2,10}-\d+$",
        RegexOptions.Compiled);

    private static readonly string[] BreakingMarkers = ["BREAKING CHANGE:", "BREAKING-CHANGE:"];

    public ParsedCommit Parse(string commitMessage)
    {
        var firstNewline = commitMessage.IndexOf('\n');
        var header = (firstNewline < 0 ? commitMessage : commitMessage[..firstNewline]).Trim();
        var body   = firstNewline < 0 ? string.Empty : commitMessage[(firstNewline + 1)..];

        var match = HeaderRegex.Match(header);
        if (!match.Success)
            return new ParsedCommit(null, null, null, false, false, null);

        var type = match.Groups["type"].Value;

        // Conventional commits spec requires lowercase types; uppercase signals non-conventional
        if (!type.All(char.IsLower))
            return new ParsedCommit(null, null, null, false, false, null);

        var scope       = match.Groups["scope"].Success ? match.Groups["scope"].Value : null;
        var description = match.Groups["desc"].Value;
        var bangBreak   = match.Groups["breaking"].Success;
        var bodyBreak   = BreakingMarkers.Any(body.Contains);
        var isBreaking  = bangBreak || bodyBreak;

        var jiraTicketId = scope != null && JiraPattern.IsMatch(scope) ? scope : null;

        return new ParsedCommit(type, scope, description, isBreaking, true, jiraTicketId);
    }
}
