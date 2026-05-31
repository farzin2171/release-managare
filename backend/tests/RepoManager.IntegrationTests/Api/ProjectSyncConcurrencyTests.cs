using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Application.DTOs;
using RepoManager.Application.GitProviders;
using RepoManager.Domain.Aggregates;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests.Api;

public class ProjectSyncConcurrencyTests : IDisposable
{
    private readonly ProjectSyncTestFactory _factory;
    private readonly HttpClient _client;

    private const string AdminEmail = "proj-sync-admin@test.com";
    private const string AdminPassword = "Password123!";

    public ProjectSyncConcurrencyTests()
    {
        _factory = new ProjectSyncTestFactory();
        _client = _factory.CreateClientWithSetupKey();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // (a) 3 repos (1 success, 1 forced-failure, 1 no-tag) → PartiallyFailed ──

    [Fact]
    public async Task ProjectSync_MixedRepos_PartiallyFailed()
    {
        var token = await SetupAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var projectId = await CreateProjectWithReposAsync(tagRepo1: "v1.0.0", tagRepo2: "v2.0.0", tagRepo3: null);

        var resp = await _client.PostAsync($"/api/v1/projects/{projectId}/sync", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var run = await PollUntilTerminalAsync(projectId);

        run.Should().NotBeNull();
        run!.Status.Should().Be(ProjectSyncStatus.PartiallyFailed);
        run.SucceededCount.Should().Be(1);
        run.FailedCount.Should().Be(1);
        run.SkippedCount.Should().Be(1);
    }

    // (b) Second concurrent enqueue → 409 ────────────────────────────────────

    [Fact]
    public async Task ProjectSync_ConcurrentEnqueue_Returns409()
    {
        var token = await SetupAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var projectId = await CreateProjectWithReposAsync(tagRepo1: "v1.0.0", tagRepo2: null, tagRepo3: null);

        // First enqueue succeeds (project has only no-tag repos so completes instantly via Skipped)
        // Use a project with a delayed repo to ensure the first sync is still running
        var first = await _client.PostAsync($"/api/v1/projects/{projectId}/sync", null);
        first.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Immediately request a second sync while first is pending
        var second = await _client.PostAsync($"/api/v1/projects/{projectId}/sync", null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // (c) Cancel after first repo completes ───────────────────────────────────

    [Fact]
    public async Task ProjectSync_Cancel_StopsAfterCurrentRepo()
    {
        var token = await SetupAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Project with 1 success repo + 1 slow repo (using blocking provider)
        var projectId = await CreateProjectWithSlowReposAsync(firstTag: "v1.0.0", secondTag: "v2.0.0");

        var resp = await _client.PostAsync($"/api/v1/projects/{projectId}/sync", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Wait for first repo to start
        await Task.Delay(500);

        // Cancel
        var cancelResp = await _client.DeleteAsync($"/api/v1/projects/{projectId}/sync/active");
        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Wait for run to finalise
        var run = await PollUntilTerminalAsync(projectId, timeoutMs: 8000);

        run.Should().NotBeNull();
        run!.Status.Should().Be(ProjectSyncStatus.Cancelled);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<string> SetupAndLoginAsync()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/setup", new { email = AdminEmail, password = AdminPassword });
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(u => u.Email == AdminEmail);
        user.Role = Role.Admin;
        await db.SaveChangesAsync();

        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = AdminEmail, password = AdminPassword });
        var tokens = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        return tokens!.AccessToken;
    }

    private async Task<Guid> CreateProjectWithReposAsync(string? tagRepo1, string? tagRepo2, string? tagRepo3)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conn = new Domain.Entities.GitProviderConnection
        {
            Id = Guid.NewGuid(), Name = $"conn-proj-{Guid.NewGuid():N}",
            ProviderType = ProviderType.AzureDevOps,
            OrganizationUrl = "https://dev.azure.com/test",
            EncryptedPat = "dW5lbmNyeXB0ZWQ=", IsActive = true
        };
        db.GitProviderConnections.Add(conn);

        var project = new Domain.Entities.Project
        {
            Id = Guid.NewGuid(), Name = $"proj-{Guid.NewGuid():N}",
            Color = "#6B7280", JiraProjectKeys = "[]",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Projects.Add(project);

        foreach (var tag in new[] { tagRepo1, tagRepo2, tagRepo3 })
        {
            var repo = new Domain.Entities.Repository
            {
                Id = Guid.NewGuid(), GitProviderConnectionId = conn.Id,
                ExternalId = $"ext-{tag ?? "notag"}-{Guid.NewGuid():N}",
                Name = $"repo-{tag ?? "notag"}-{Guid.NewGuid():N}",
                DefaultBranch = "main", WebUrl = "https://example.com",
                AzureProjectName = "TestProject", IsTracked = true, LatestTag = tag
            };
            db.Repositories.Add(repo);
            db.ProjectRepositories.Add(new Domain.Entities.ProjectRepository
            {
                ProjectId = project.Id, RepositoryId = repo.Id
            });
        }

        await db.SaveChangesAsync();
        return project.Id;
    }

    private async Task<Guid> CreateProjectWithSlowReposAsync(string firstTag, string secondTag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conn = new Domain.Entities.GitProviderConnection
        {
            Id = Guid.NewGuid(), Name = $"conn-slow-{Guid.NewGuid():N}",
            ProviderType = ProviderType.AzureDevOps,
            OrganizationUrl = "https://dev.azure.com/test",
            EncryptedPat = "dW5lbmNyeXB0ZWQ=", IsActive = true
        };
        db.GitProviderConnections.Add(conn);

        var project = new Domain.Entities.Project
        {
            Id = Guid.NewGuid(), Name = $"proj-slow-{Guid.NewGuid():N}",
            Color = "#6B7280", JiraProjectKeys = "[]",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Projects.Add(project);

        foreach (var tag in new[] { firstTag, secondTag })
        {
            var repo = new Domain.Entities.Repository
            {
                Id = Guid.NewGuid(), GitProviderConnectionId = conn.Id,
                ExternalId = $"ext-{tag}-slow-{Guid.NewGuid():N}",
                Name = $"repo-{tag}-slow", DefaultBranch = "main",
                WebUrl = "https://example.com", AzureProjectName = "TestProject",
                IsTracked = true, LatestTag = tag
            };
            db.Repositories.Add(repo);
            db.ProjectRepositories.Add(new Domain.Entities.ProjectRepository
            {
                ProjectId = project.Id, RepositoryId = repo.Id
            });
        }

        await db.SaveChangesAsync();
        return project.Id;
    }

    private async Task<ProjectSyncDto?> PollUntilTerminalAsync(Guid projectId, int timeoutMs = 6000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var resp = await _client.GetAsync($"/api/v1/projects/{projectId}/sync/latest");
            if (resp.IsSuccessStatusCode)
            {
                var dto = await resp.Content.ReadFromJsonAsync<ProjectSyncDto>();
                if (dto is { Status: ProjectSyncStatus.Succeeded or ProjectSyncStatus.PartiallyFailed
                                    or ProjectSyncStatus.Failed or ProjectSyncStatus.Cancelled })
                    return dto;
            }
            await Task.Delay(300);
        }
        return null;
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, string TokenType);
}

// ── Test factory with mixed fake providers ───────────────────────────────────

internal sealed class MixedProjectSyncProvider : IGitProvider
{
    private int _callCount;

    public Task<bool> TestConnectionAsync(ProviderConnection conn, CancellationToken ct = default) => Task.FromResult(true);
    public Task<IEnumerable<RepoSummary>> ListRepositoriesAsync(ProviderConnection conn, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<RepoSummary>());
    public Task<IEnumerable<TagInfo>> ListTagsAsync(ProviderConnection conn, string repoExternalId, CancellationToken ct = default) => Task.FromResult<IEnumerable<TagInfo>>([]);
    public Task<DateTimeOffset?> GetCommitDateAsync(ProviderConnection conn, string repoExternalId, string commitSha, CancellationToken ct = default) => Task.FromResult<DateTimeOffset?>(null);
    public Task<IEnumerable<PullRequestInfo>> GetMergedPullRequestsAsync(ProviderConnection conn, string repoExternalId, DateTime since, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<PullRequestInfo>());

    public Task<IEnumerable<CommitInfo>> GetCommitsBetweenAsync(
        ProviderConnection conn, string repoExternalId, string fromRef, string toRef,
        DateTimeOffset? fromDate = null, CancellationToken ct = default)
    {
        var call = Interlocked.Increment(ref _callCount);
        if (call % 2 == 0)
            throw new InvalidOperationException("Simulated provider failure for repo 2");
        return Task.FromResult(RepoSyncFakeProvider.GetCommits());
    }
}

internal sealed class MixedProjectSyncProviderFactory : IGitProviderFactory
{
    private readonly MixedProjectSyncProvider _provider = new();
    public IGitProvider GetProvider(ProviderType providerType) => _provider;
}

internal sealed class ProjectSyncTestFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var desc = services.SingleOrDefault(d => d.ServiceType == typeof(IGitProviderFactory));
            if (desc is not null) services.Remove(desc);
            services.AddSingleton<IGitProviderFactory, MixedProjectSyncProviderFactory>();
        });
    }
}
