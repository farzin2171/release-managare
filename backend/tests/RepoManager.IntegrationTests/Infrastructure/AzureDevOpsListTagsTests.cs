using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.GitProviders;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.GitProviders;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace RepoManager.IntegrationTests.Infrastructure;

public class AzureDevOpsListTagsTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly AzureDevOpsGitProvider _provider;

    private const string RepoId = "test-repo-external-id";
    private const string AnnotatedCommitSha = "def9876543210fedcba9876543210fedcba987654";
    private const string LightweightObjectId = "abc1234567890abcdef1234567890abcdef123456";

    public AzureDevOpsListTagsTests()
    {
        _server = WireMockServer.Start();
        _provider = new AzureDevOpsGitProvider(NullLogger<AzureDevOpsGitProvider>.Instance);

        // Catch-all fallback: handles any SDK pre-flight calls (connectionData, resourceAreas, etc.)
        _server.Given(Request.Create().UsingAnyMethod())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("{\"value\":[],\"count\":0}"));
    }

    public void Dispose() => _server.Stop();

    private ProviderConnection Conn() => new(
        OrganizationUrl: _server.Urls[0],
        DecryptedPat: "test-pat",
        Type: ProviderType.AzureDevOps);

    private void StubRefs(string refsJson)
    {
        _server.Given(
            Request.Create()
                .WithPath(new WireMock.Matchers.WildcardMatcher($"*repositories/{RepoId}/refs*"))
                .UsingGet())
            .AtPriority(10)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(refsJson));
    }

    private void StubCommit(string commitSha, string authorName, string authorDate)
    {
        _server.Given(
            Request.Create()
                .WithPath(new WireMock.Matchers.WildcardMatcher($"*repositories/{RepoId}/commits/{commitSha}*"))
                .UsingGet())
            .AtPriority(10)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody($$"""
                        {
                          "commitId": "{{commitSha}}",
                          "author": { "name": "{{authorName}}", "email": "{{authorName.ToLower()}}@example.com", "date": "{{authorDate}}" }
                        }
                        """));
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ListTagsAsync_EmptyTagList_ReturnsEmptyCollection()
    {
        StubRefs("""{"value":[],"count":0}""");

        var result = await _provider.ListTagsAsync(Conn(), RepoId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ListTagsAsync_AnnotatedTag_UsesPeeledObjectId()
    {
        StubRefs($$"""
            {
              "value": [{
                "name": "refs/tags/v1.0.0",
                "objectId": "{{LightweightObjectId}}",
                "peeledObjectId": "{{AnnotatedCommitSha}}"
              }],
              "count": 1
            }
            """);
        StubCommit(AnnotatedCommitSha, "Jane Smith", "2026-05-01T10:00:00Z");

        var result = (await _provider.ListTagsAsync(Conn(), RepoId)).ToList();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("v1.0.0");
        result[0].CommitSha.Should().Be(AnnotatedCommitSha);
        result[0].AuthorName.Should().Be("Jane Smith");
    }

    [Fact]
    public async Task ListTagsAsync_LightweightTag_FallsBackToObjectId()
    {
        StubRefs($$"""
            {
              "value": [{
                "name": "refs/tags/v2.0.0",
                "objectId": "{{LightweightObjectId}}"
              }],
              "count": 1
            }
            """);
        StubCommit(LightweightObjectId, "John Doe", "2026-05-10T08:00:00Z");

        var result = (await _provider.ListTagsAsync(Conn(), RepoId)).ToList();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("v2.0.0");
        result[0].CommitSha.Should().Be(LightweightObjectId);
        result[0].AuthorName.Should().Be("John Doe");
    }

    [Fact]
    public async Task ListTagsAsync_ProviderReturns401_ThrowsExternalServiceException()
    {
        _server.Given(
            Request.Create()
                .WithPath(new WireMock.Matchers.WildcardMatcher($"*repositories/{RepoId}/refs*"))
                .UsingGet())
            .AtPriority(10)
            .RespondWith(
                Response.Create()
                    .WithStatusCode(401)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody("""{"message":"TF400813: The user '' is not authorized to access this resource."}"""));

        var act = async () => await _provider.ListTagsAsync(Conn(), RepoId);

        await act.Should().ThrowAsync<ExternalServiceException>()
            .WithMessage("*AzureDevOps*");
    }

    [Fact]
    public async Task ListTagsAsync_NetworkTimeout_ThrowsExternalServiceException()
    {
        using var cts = new CancellationTokenSource(TimeSpan.Zero);

        var act = async () => await _provider.ListTagsAsync(Conn(), RepoId, cts.Token);

        await act.Should().ThrowAsync<ExternalServiceException>()
            .WithMessage("*AzureDevOps*");
    }
}
