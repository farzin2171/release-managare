using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Application.DTOs;
using RepoManager.Application.GitProviders;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;
using RepoManager.Application.Services;

namespace RepoManager.IntegrationTests.Api;

public class RepositorySyncIntegrationTests : IDisposable
{
    private readonly RepoSyncTestFactory _factory;
    private readonly HttpClient _adminClient;

    private const string AdminEmail = "sync-int-admin@test.com";
    private const string AdminPassword = "Password123!";

    public RepositorySyncIntegrationTests()
    {
        _factory = new RepoSyncTestFactory();
        _adminClient = _factory.CreateClient();
    }

    public void Dispose()
    {
        _adminClient.Dispose();
        _factory.Dispose();
    }

    // (a) Fake provider returns commits → sync → assert counts and DB rows ───

    [Fact]
    public async Task Sync_WithCommits_PersistsCountsAndRows()
    {
        await SetupAdminAsync();
        var token = await LoginAsync(AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var repoId = await CreateRepoWithTagAsync("v1.0.0");

        // POST sync
        var postResp = await _adminClient.PostAsync($"/api/v1/repositories/{repoId}/sync", null);
        postResp.StatusCode.Should().Be(HttpStatusCode.Accepted);

        var dto = await postResp.Content.ReadFromJsonAsync<RepositorySyncDto>();
        dto.Should().NotBeNull();

        // Wait for completion
        var finalDto = await PollUntilTerminalAsync(repoId);

        finalDto!.Status.Should().Be(SyncStatus.Succeeded);
        finalDto.CommitCount.Should().Be(RepoSyncFakeProvider.CommitCount);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Commits.Where(c => c.RepositoryId == repoId).Should().HaveCount(RepoSyncFakeProvider.CommitCount);
    }

    // (b) Re-sync same (repo, tag) → idempotency ────────────────────────────

    [Fact]
    public async Task Sync_Twice_IsIdempotent_NoNewCommitRows()
    {
        await SetupAdminAsync();
        var token = await LoginAsync(AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var repoId = await CreateRepoWithTagAsync("v2.0.0");

        await _adminClient.PostAsync($"/api/v1/repositories/{repoId}/sync", null);
        await PollUntilTerminalAsync(repoId);

        await _adminClient.PostAsync($"/api/v1/repositories/{repoId}/sync", null);
        await PollUntilTerminalAsync(repoId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Commits.Where(c => c.RepositoryId == repoId).Should().HaveCount(RepoSyncFakeProvider.CommitCount);
    }

    // (c) Five step events published ─────────────────────────────────────────

    [Fact]
    public async Task Sync_PublishesFiveStepEvents()
    {
        await SetupAdminAsync();
        var token = await LoginAsync(AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var repoId = await CreateRepoWithTagAsync("v3.0.0");

        using var scope = _factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<Application.Services.IRepositorySyncService>();
        var userId = (await GetAdminUserIdAsync())!.Value;

        var eventPublisher = _factory.Services.GetRequiredService<Application.Events.ISyncEventPublisher>();
        var dto = await syncService.EnqueueAsync(repoId, userId);

        var events = new List<Application.Events.SyncEvent>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        try
        {
            await foreach (var evt in eventPublisher.SubscribeAsync(dto.Id, cts.Token))
                events.Add(evt);
        }
        catch (OperationCanceledException) { }

        events.Select(e => e.CurrentStep).Should().Contain([
            SyncStep.FetchingCommits,
            SyncStep.ParsingCommits,
            SyncStep.PersistingCommits,
            SyncStep.AggregatingTickets,
            SyncStep.Finalising
        ]);
    }

    // (d) Concurrent sync → 409 ──────────────────────────────────────────────

    [Fact]
    public async Task ConcurrentSync_Returns409()
    {
        await SetupAdminAsync();
        var token = await LoginAsync(AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var repoId = await CreateRepoWithTagAsync("v4.0.0");

        // First request — goes in pending
        var first = await _adminClient.PostAsync($"/api/v1/repositories/{repoId}/sync", null);
        first.StatusCode.Should().Be(HttpStatusCode.Accepted);

        // Immediate second request while first is pending — should 409
        var second = await _adminClient.PostAsync($"/api/v1/repositories/{repoId}/sync", null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task SetupAdminAsync()
    {
        await _adminClient.PostAsJsonAsync("/api/v1/auth/setup", new { email = AdminEmail, password = AdminPassword });
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(u => u.Email == AdminEmail);
        user.Role = Domain.Enums.Role.Admin;
        await db.SaveChangesAsync();
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var resp = await _adminClient.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var tokens = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        return tokens!.AccessToken;
    }

    private async Task<Guid> CreateRepoWithTagAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conn = new Domain.Entities.GitProviderConnection
        {
            Id = Guid.NewGuid(),
            Name = $"conn-{tag}",
            ProviderType = Domain.Enums.ProviderType.AzureDevOps,
            OrganizationUrl = "https://dev.azure.com/test",
            EncryptedPat = "dW5lbmNyeXB0ZWQ=",
            IsActive = true
        };
        db.GitProviderConnections.Add(conn);

        var repo = new Domain.Entities.Repository
        {
            Id = Guid.NewGuid(),
            GitProviderConnectionId = conn.Id,
            ExternalId = $"ext-{tag}-{Guid.NewGuid():N}",
            Name = $"repo-{tag}",
            DefaultBranch = "main",
            WebUrl = "https://example.com",
            AzureProjectName = "TestProject",
            IsTracked = true,
            LatestTag = tag
        };
        db.Repositories.Add(repo);
        await db.SaveChangesAsync();
        return repo.Id;
    }

    private async Task<RepositorySyncDto?> PollUntilTerminalAsync(Guid repoId, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var resp = await _adminClient.GetAsync($"/api/v1/repositories/{repoId}/sync/latest");
            if (resp.IsSuccessStatusCode)
            {
                var dto = await resp.Content.ReadFromJsonAsync<RepositorySyncDto>();
                if (dto is { Status: SyncStatus.Succeeded or SyncStatus.Failed or SyncStatus.Skipped })
                    return dto;
            }
            await Task.Delay(200);
        }
        return null;
    }

    private async Task<Guid?> GetAdminUserIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Users.FirstOrDefault(u => u.Email == AdminEmail)?.Id;
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, string TokenType);
}

// ── Fake provider with deterministic commits ──────────────────────────────────

internal static class RepoSyncFakeProvider
{
    public const int CommitCount = 3;

    public static IEnumerable<CommitInfo> GetCommits() =>
    [
        new("sha001aaa", "feat(auth): add login", "Alice", "alice@example.com", DateTimeOffset.UtcNow),
        new("sha002bbb", "fix: null ref in service", "Bob", "bob@example.com", DateTimeOffset.UtcNow.AddMinutes(-1)),
        new("sha003ccc", "chore: update deps", "Alice", "alice@example.com", DateTimeOffset.UtcNow.AddMinutes(-2))
    ];
}

internal class FakeRepoSyncProvider : IGitProvider
{
    public Task<bool> TestConnectionAsync(ProviderConnection conn, CancellationToken ct = default) => Task.FromResult(true);
    public Task<IEnumerable<RepoSummary>> ListRepositoriesAsync(ProviderConnection conn, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<RepoSummary>());
    public Task<IEnumerable<TagInfo>> ListTagsAsync(ProviderConnection conn, string repoExternalId, CancellationToken ct = default) => Task.FromResult<IEnumerable<TagInfo>>([]);
    public Task<DateTimeOffset?> GetCommitDateAsync(ProviderConnection conn, string repoExternalId, string commitSha, CancellationToken ct = default) => Task.FromResult<DateTimeOffset?>(null);
    public Task<IEnumerable<CommitInfo>> GetCommitsBetweenAsync(ProviderConnection conn, string repoExternalId, string fromRef, string toRef, DateTimeOffset? fromDate = null, CancellationToken ct = default) => Task.FromResult(RepoSyncFakeProvider.GetCommits());
    public Task<IEnumerable<PullRequestInfo>> GetMergedPullRequestsAsync(ProviderConnection conn, string repoExternalId, DateTime since, CancellationToken ct = default) => Task.FromResult(Enumerable.Empty<PullRequestInfo>());
}

internal class FakeRepoSyncProviderFactory : IGitProviderFactory
{
    private readonly FakeRepoSyncProvider _provider = new();
    public IGitProvider GetProvider(Domain.Enums.ProviderType providerType) => _provider;
}

internal class RepoSyncTestFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var desc = services.SingleOrDefault(d => d.ServiceType == typeof(IGitProviderFactory));
            if (desc is not null) services.Remove(desc);
            services.AddSingleton<IGitProviderFactory, FakeRepoSyncProviderFactory>();
        });
    }
}

// ── Snapshot endpoint integration tests ───────────────────────────────────────

public class SnapshotIntegrationTests : IDisposable
{
    private readonly RepoSyncTestFactory _factory;
    private readonly HttpClient _client;
    private const string AdminEmail = "snapshot-admin@test.com";
    private const string AdminPassword = "Password123!";

    public SnapshotIntegrationTests()
    {
        _factory = new RepoSyncTestFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Snapshot_AfterSync_ShowsCorrectData()
    {
        await SetupAdminAsync();
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var (projectId, repoId) = await CreateProjectWithRepoAsync("v5.0.0");

        // Before sync — latestSync should be null
        var pre = await GetSnapshotAsync(projectId);
        pre.Should().HaveCount(1);
        pre![0].LatestSync.Should().BeNull();

        // Trigger and wait for sync
        await _client.PostAsync($"/api/v1/repositories/{repoId}/sync", null);
        await PollUntilTerminalAsync(repoId);

        // After sync — latestSync should show data and cache should be fresh
        var post = await GetSnapshotAsync(projectId);
        post.Should().HaveCount(1);
        post![0].LatestSync.Should().NotBeNull();
        post![0].LatestSync!.Status.Should().Be(SyncStatus.Succeeded);
        post![0].LatestSync!.CommitCount.Should().Be(RepoSyncFakeProvider.CommitCount);
    }

    [Fact]
    public async Task Snapshot_AfterTagChange_LatestSyncBecomesNull()
    {
        await SetupAdminAsync();
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var (projectId, repoId) = await CreateProjectWithRepoAsync("v6.0.0");

        // Sync with tag v6.0.0
        await _client.PostAsync($"/api/v1/repositories/{repoId}/sync", null);
        await PollUntilTerminalAsync(repoId);

        var afterSync = await GetSnapshotAsync(projectId);
        afterSync![0].LatestSync.Should().NotBeNull();
        afterSync![0].LatestTag.Should().Be("v6.0.0");

        // Change the tag on the repo
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var repo = db.Repositories.Single(r => r.Id == repoId);
            repo.LatestTag = "v7.0.0";
            await db.SaveChangesAsync();
        }

        // Invalidate cache (simulate what would happen on the next request after a tag change)
        using (var scope = _factory.Services.CreateScope())
        {
            var svc = scope.ServiceProvider.GetRequiredService<Application.Services.IProjectSyncSnapshotService>();
            svc.InvalidateCache(projectId);
        }

        // Snapshot should now show null LatestSync because tag changed
        var afterTagChange = await GetSnapshotAsync(projectId);
        afterTagChange![0].LatestSync.Should().BeNull();
        afterTagChange![0].LatestTag.Should().Be("v7.0.0");
    }

    [Fact]
    public async Task Snapshot_CacheInvalidatesOnSyncCompletion()
    {
        await SetupAdminAsync();
        var token = await LoginAsync();
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var (projectId, repoId) = await CreateProjectWithRepoAsync("v8.0.0");

        // Prime the cache with a "not synced" snapshot
        var initial = await GetSnapshotAsync(projectId);
        initial![0].LatestSync.Should().BeNull();

        // Sync completes — this should invalidate the cache
        await _client.PostAsync($"/api/v1/repositories/{repoId}/sync", null);
        await PollUntilTerminalAsync(repoId);

        // If cache was properly invalidated, the fresh call should see the completed sync
        var fresh = await GetSnapshotAsync(projectId);
        fresh![0].LatestSync.Should().NotBeNull("cache should have been invalidated by sync completion");
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task SetupAdminAsync()
    {
        await _client.PostAsJsonAsync("/api/v1/auth/setup", new { email = AdminEmail, password = AdminPassword });
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = db.Users.Single(u => u.Email == AdminEmail);
        user.Role = Domain.Enums.Role.Admin;
        await db.SaveChangesAsync();
    }

    private async Task<string> LoginAsync()
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email = AdminEmail, password = AdminPassword });
        var tokens = await resp.Content.ReadFromJsonAsync<SnapshotTokenResponse>();
        return tokens!.AccessToken;
    }

    private async Task<(Guid ProjectId, Guid RepoId)> CreateProjectWithRepoAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conn = new Domain.Entities.GitProviderConnection
        {
            Id = Guid.NewGuid(),
            Name = $"conn-snap-{tag}",
            ProviderType = Domain.Enums.ProviderType.AzureDevOps,
            OrganizationUrl = "https://dev.azure.com/test",
            EncryptedPat = "dW5lbmNyeXB0ZWQ=",
            IsActive = true
        };
        db.GitProviderConnections.Add(conn);

        var repo = new Domain.Entities.Repository
        {
            Id = Guid.NewGuid(),
            GitProviderConnectionId = conn.Id,
            ExternalId = $"ext-snap-{tag}-{Guid.NewGuid():N}",
            Name = $"repo-snap-{tag}",
            DefaultBranch = "main",
            WebUrl = "https://example.com",
            AzureProjectName = "TestProject",
            IsTracked = true,
            LatestTag = tag
        };
        db.Repositories.Add(repo);

        var project = new Domain.Entities.Project
        {
            Id = Guid.NewGuid(),
            Name = $"proj-snap-{tag}-{Guid.NewGuid():N}",
            Color = "#3B82F6",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Projects.Add(project);

        db.ProjectRepositories.Add(new Domain.Entities.ProjectRepository
        {
            ProjectId = project.Id,
            RepositoryId = repo.Id
        });

        await db.SaveChangesAsync();
        return (project.Id, repo.Id);
    }

    private async Task<List<RepoSyncSnapshotItemDto>?> GetSnapshotAsync(Guid projectId)
    {
        var resp = await _client.GetAsync($"/api/v1/projects/{projectId}/repositories/sync-snapshot");
        resp.IsSuccessStatusCode.Should().BeTrue();
        return await resp.Content.ReadFromJsonAsync<List<RepoSyncSnapshotItemDto>>();
    }

    private async Task<RepositorySyncDto?> PollUntilTerminalAsync(Guid repoId, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var resp = await _client.GetAsync($"/api/v1/repositories/{repoId}/sync/latest");
            if (resp.IsSuccessStatusCode)
            {
                var dto = await resp.Content.ReadFromJsonAsync<RepositorySyncDto>();
                if (dto is { Status: SyncStatus.Succeeded or SyncStatus.Failed or SyncStatus.Skipped })
                    return dto;
            }
            await Task.Delay(200);
        }
        return null;
    }

    private sealed record SnapshotTokenResponse(string AccessToken, string RefreshToken, string TokenType);
}
