using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests.Releases;

/// <summary>
/// Integration test: full create-release composition flow via the HTTP API.
/// Each test gets an isolated in-memory SQLite DB.
/// </summary>
public class CreateReleaseFlowTests : IDisposable
{
    private readonly ReleaseFlowFactory _factory;
    private readonly HttpClient _client;
    private string? _adminToken;

    private const string AdminEmail = "release-admin@test.com";
    private const string AdminPassword = "Password123!";

    public CreateReleaseFlowTests()
    {
        _factory = new ReleaseFlowFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task CreateRelease_WithTwoRepos_SnapshotFieldsPopulated()
    {
        await SetupAdminAsync();
        var (projectId, repoA, repoB) = await SeedProjectWithReposAsync(primaryRepoId: null);

        // POST /releases
        var createResp = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/releases",
            new
            {
                name = "Test Release",
                repositories = new[]
                {
                    new { repositoryId = repoA, nextVersion = "1.1.0", bumpType = "minor" },
                    new { repositoryId = repoB, nextVersion = "2.0.0", bumpType = "major" }
                }
            });

        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await createResp.Content.ReadFromJsonAsync<ReleaseCompositionResponse>();
        body.Should().NotBeNull();
        body!.Name.Should().Be("Test Release");
        body.Status.Should().Be("Draft");
        body.ReleaseRepositories.Should().HaveCount(2);

        var rA = body.ReleaseRepositories.Single(r => r.RepositoryId == repoA);
        rA.NextVersion.Should().Be("1.1.0");
        rA.BumpType.Should().Be("minor");

        var rB = body.ReleaseRepositories.Single(r => r.RepositoryId == repoB);
        rB.NextVersion.Should().Be("2.0.0");
        rB.BumpType.Should().Be("major");

        var releaseId = body.Id;

        // GET /releases/{id} — snapshot unchanged
        var getResp = await _client.GetAsync($"/api/v1/projects/{projectId}/releases/{releaseId}");
        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResp.Content.ReadFromJsonAsync<ReleaseCompositionResponse>();
        getBody!.ReleaseRepositories.Single(r => r.RepositoryId == repoA).NextVersion.Should().Be("1.1.0");

        // PUT /releases/{id} — update draft
        var putResp = await _client.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/releases/{releaseId}",
            new
            {
                repositories = new[]
                {
                    new { repositoryId = repoA, nextVersion = "1.2.0", bumpType = "minor" }
                }
            });
        putResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var putBody = await putResp.Content.ReadFromJsonAsync<ReleaseCompositionResponse>();
        putBody!.ReleaseRepositories.Should().HaveCount(1);
        putBody.ReleaseRepositories[0].NextVersion.Should().Be("1.2.0");

        // DELETE /releases/{id}
        var delResp = await _client.DeleteAsync($"/api/v1/projects/{projectId}/releases/{releaseId}");
        delResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Confirm it's gone
        var getAfterDel = await _client.GetAsync($"/api/v1/projects/{projectId}/releases/{releaseId}");
        getAfterDel.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateDraft_WhenPublished_Returns409WithCode()
    {
        await SetupAdminAsync();
        var (projectId, repoA, _) = await SeedProjectWithReposAsync(primaryRepoId: null);

        // Create draft
        var createResp = await _client.PostAsJsonAsync(
            $"/api/v1/projects/{projectId}/releases",
            new
            {
                name = "Published Release",
                repositories = new[] { new { repositoryId = repoA, nextVersion = "1.0.1", bumpType = "patch" } }
            });
        var body = await createResp.Content.ReadFromJsonAsync<ReleaseCompositionResponse>();
        var releaseId = body!.Id;

        // Manually publish the release in the DB
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var release = await db.Releases.FindAsync(releaseId);
            release!.Status = ReleaseStatus.Published;
            await db.SaveChangesAsync();
        }

        // Attempt to PUT — should return 409
        var putResp = await _client.PutAsJsonAsync(
            $"/api/v1/projects/{projectId}/releases/{releaseId}",
            new
            {
                repositories = new[] { new { repositoryId = repoA, nextVersion = "1.0.2", bumpType = "patch" } }
            });
        putResp.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await putResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.TryGetProperty("code", out var codeProp).Should().BeTrue();
        codeProp.GetString().Should().Be("release_not_draft");
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private async Task SetupAdminAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/setup",
            new { email = AdminEmail, password = AdminPassword });
        resp.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Gone);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = db.Users.Single(u => u.Email == AdminEmail);
        admin.Role = Role.Admin;
        await db.SaveChangesAsync();

        var loginResp = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = AdminEmail, password = AdminPassword });
        var tokens = await loginResp.Content.ReadFromJsonAsync<TokensResponse>();
        _adminToken = tokens!.AccessToken;
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
    }

    private async Task<(Guid projectId, Guid repoA, Guid repoB)> SeedProjectWithReposAsync(Guid? primaryRepoId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conn = new GitProviderConnection
        {
            Id = Guid.NewGuid(), Name = "test-conn",
            ProviderType = ProviderType.AzureDevOps,
            OrganizationUrl = "https://dev.azure.com/test",
            EncryptedPat = "dW5lbmNyeXB0ZWQ=",
            IsActive = true
        };
        db.GitProviderConnections.Add(conn);

        var repoA = new Repository
        {
            Id = Guid.NewGuid(), ExternalId = "ext-a", Name = "RepoA",
            DefaultBranch = "main", WebUrl = "https://example.com/a",
            AzureProjectName = "test", GitProviderConnectionId = conn.Id
        };
        var repoB = new Repository
        {
            Id = Guid.NewGuid(), ExternalId = "ext-b", Name = "RepoB",
            DefaultBranch = "main", WebUrl = "https://example.com/b",
            AzureProjectName = "test", GitProviderConnectionId = conn.Id
        };
        db.Repositories.AddRange(repoA, repoB);

        var project = new Project
        {
            Id = Guid.NewGuid(), Name = $"Test Project {Guid.NewGuid():N}",
            Color = "#3B82F6", JiraProjectKeys = "[]",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Projects.Add(project);

        db.ProjectRepositories.AddRange(
            new ProjectRepository { ProjectId = project.Id, RepositoryId = repoA.Id, IsPrimary = primaryRepoId == repoA.Id },
            new ProjectRepository { ProjectId = project.Id, RepositoryId = repoB.Id, IsPrimary = primaryRepoId == repoB.Id }
        );

        await db.SaveChangesAsync();
        return (project.Id, repoA.Id, repoB.Id);
    }

    private sealed record TokensResponse(string AccessToken, string RefreshToken, string TokenType);

    private sealed record ReleaseCompositionResponse(
        Guid Id, string Name, string Version, string Status,
        List<ReleaseRepoItem> ReleaseRepositories);

    private sealed record ReleaseRepoItem(
        Guid Id, Guid RepositoryId, string RepositoryName,
        string PreviousVersion, string NextVersion, string BumpType,
        string FromCommitSha, string ToCommitSha,
        int CommitCount, int TicketCount, bool IsLegacy);
}

/// <summary>Factory with an isolated in-memory SQLite connection per test class instance.</summary>
internal sealed class ReleaseFlowFactory : TestWebApplicationFactory { }
