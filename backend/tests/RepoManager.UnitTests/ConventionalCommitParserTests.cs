using FluentAssertions;
using RepoManager.Infrastructure.Commits;

namespace RepoManager.UnitTests;

public class ConventionalCommitParserTests
{
    private readonly ConventionalCommitParser _parser = new();

    // --- All 12 standard lowercase types ---

    [Theory]
    [InlineData("feat: add dark mode", "feat")]
    [InlineData("fix: resolve null ref", "fix")]
    [InlineData("docs: update README", "docs")]
    [InlineData("style: format whitespace", "style")]
    [InlineData("refactor: extract helper", "refactor")]
    [InlineData("perf: cache query results", "perf")]
    [InlineData("test: add unit coverage", "test")]
    [InlineData("build: upgrade dotnet", "build")]
    [InlineData("ci: add github workflow", "ci")]
    [InlineData("chore: bump semver", "chore")]
    [InlineData("revert: revert feat ABC", "revert")]
    [InlineData("unknown: miscellaneous", "unknown")]
    public void Parse_StandardLowercaseTypes_ReturnsConventionalWithCorrectType(string message, string expectedType)
    {
        var result = _parser.Parse(message);

        result.IsConventional.Should().BeTrue();
        result.Type.Should().Be(expectedType);
        result.IsBreaking.Should().BeFalse();
    }

    // --- Scope matching Jira pattern ^[A-Z]{2,10}-\d+$ ---

    [Theory]
    [InlineData("feat(APPLY-123): add feature", "APPLY-123")]
    [InlineData("fix(AB-1): minimal key", "AB-1")]
    [InlineData("chore(ABCDEFGHIJ-9999): ten char key", "ABCDEFGHIJ-9999")]
    public void Parse_JiraScope_PopulatesJiraTicketId(string message, string expectedTicketId)
    {
        var result = _parser.Parse(message);

        result.IsConventional.Should().BeTrue();
        result.Scope.Should().Be(expectedTicketId);
        result.JiraTicketId.Should().Be(expectedTicketId);
    }

    // --- Scope NOT matching Jira pattern ---

    [Theory]
    [InlineData("feat(api): add endpoint", "api")]
    [InlineData("fix(A-123): one char prefix", "A-123")]
    [InlineData("chore(ABCDEFGHIJK-1): eleven char prefix", "ABCDEFGHIJK-1")]
    [InlineData("refactor(auth-module): kebab scope", "auth-module")]
    public void Parse_NonJiraScope_ScopeSetButJiraTicketIdNull(string message, string expectedScope)
    {
        var result = _parser.Parse(message);

        result.IsConventional.Should().BeTrue();
        result.Scope.Should().Be(expectedScope);
        result.JiraTicketId.Should().BeNull();
    }

    // --- Breaking via ! in header ---

    [Fact]
    public void Parse_ExclamationBreaking_IsBreakingTrue()
    {
        var result = _parser.Parse("feat!: remove legacy endpoint");

        result.IsConventional.Should().BeTrue();
        result.IsBreaking.Should().BeTrue();
        result.Type.Should().Be("feat");
    }

    [Fact]
    public void Parse_ScopeAndExclamationBreaking_IsBreakingTrue()
    {
        var result = _parser.Parse("feat(APPLY-123)!: breaking change with scope");

        result.IsConventional.Should().BeTrue();
        result.IsBreaking.Should().BeTrue();
        result.JiraTicketId.Should().Be("APPLY-123");
    }

    // --- Breaking via BREAKING CHANGE: in body ---

    [Fact]
    public void Parse_BreakingChangeColonInBody_IsBreakingTrue()
    {
        var message = "feat: new auth flow\n\nBREAKING CHANGE: replaces old token format";
        var result = _parser.Parse(message);

        result.IsConventional.Should().BeTrue();
        result.IsBreaking.Should().BeTrue();
    }

    [Fact]
    public void Parse_BreakingHyphenChangeInBody_IsBreakingTrue()
    {
        var message = "feat: new auth flow\n\nBREAKING-CHANGE: replaces old token format";
        var result = _parser.Parse(message);

        result.IsConventional.Should().BeTrue();
        result.IsBreaking.Should().BeTrue();
    }

    // --- Multi-line bodies ---

    [Fact]
    public void Parse_MultiLineBody_ParsedCorrectlyNotBreaking()
    {
        var message = "feat: add export\n\nThis exports data to CSV.\nIt supports multiple formats.";
        var result = _parser.Parse(message);

        result.IsConventional.Should().BeTrue();
        result.IsBreaking.Should().BeFalse();
        result.Type.Should().Be("feat");
        result.Description.Should().Be("add export");
    }

    [Fact]
    public void Parse_MultiLineBodyWithBreakingChange_IsBreakingTrue()
    {
        var message = "feat: overhaul API\n\nIntroduces new endpoint structure.\nBREAKING CHANGE: all v1 endpoints removed\nMigrate to v2.";
        var result = _parser.Parse(message);

        result.IsConventional.Should().BeTrue();
        result.IsBreaking.Should().BeTrue();
    }

    // --- Empty / header-only messages ---

    [Fact]
    public void Parse_HeaderOnly_EmptyBodyNotBreaking()
    {
        var result = _parser.Parse("fix: resolve timeout");

        result.IsConventional.Should().BeTrue();
        result.IsBreaking.Should().BeFalse();
        result.Description.Should().Be("resolve timeout");
    }

    // --- Non-conventional messages ---

    [Theory]
    [InlineData("WIP: blah")]
    [InlineData("fix stuff")]
    [InlineData("Merge pull request #123")]
    public void Parse_NonConventionalMessages_IsConventionalFalse(string message)
    {
        var result = _parser.Parse(message);

        result.IsConventional.Should().BeFalse();
        result.Type.Should().BeNull();
        result.JiraTicketId.Should().BeNull();
    }

    // --- Description capture ---

    [Fact]
    public void Parse_ConventionalCommit_DescriptionCaptured()
    {
        var result = _parser.Parse("feat(CORE-42): implement search indexing");

        result.Description.Should().Be("implement search indexing");
        result.Scope.Should().Be("CORE-42");
        result.JiraTicketId.Should().Be("CORE-42");
    }
}
