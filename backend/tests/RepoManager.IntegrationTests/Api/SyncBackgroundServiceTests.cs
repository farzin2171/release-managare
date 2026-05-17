using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Application.Events;
using RepoManager.Application.GitProviders;
using RepoManager.Application.Services;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests.Api;

public class SyncBackgroundServiceTests : IDisposable
{
    private readonly SyncTestFactory _factory;
    private readonly HttpClient _client;

    private const string AdminEmail = "sync-bg-admin@test.com";
    private const string AdminPassword = "Password123!";

    public SyncBackgroundServiceTests()
    {
        _factory = new SyncTestFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Worker_PicksUpJob_WithinThreeSeconds_AndPublishesEvent()
    {
        // Arrange
        await SetupAdminAsync();
        var token = await LoginAsync(AdminEmail, AdminPassword);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (repoId, userId) = await CreateRepoWithTagAsync("v1.0.0");

        var eventPublisher = _factory.Services.GetRequiredService<ISyncEventPublisher>();

        // Enqueue via the service so we get the syncId before the worker picks it up
        using var scope = _factory.Services.CreateScope();
        var scopedSyncService = scope.ServiceProvider.GetRequiredService<IRepositorySyncService>();
        var dto = await scopedSyncService.EnqueueAsync(repoId, userId);
        var syncId = dto.Id;

        var receivedEvents = new List<SyncEvent>();

        // Act — subscribe and wait up to 3s for the worker to process the job
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await foreach (var evt in eventPublisher.SubscribeAsync(syncId, cts.Token))
                receivedEvents.Add(evt);
        }
        catch (OperationCanceledException) { }

        // Assert — at least one step event was published
        receivedEvents.Should().NotBeEmpty("worker should process the job and publish step events within 3s");

        // Verify DB row reached a terminal status
        using var verifyScope = _factory.Services.CreateScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var syncRow = await db.RepositorySyncs.FindAsync([syncId]);
        syncRow!.Status.Should().BeOneOf(SyncStatus.Succeeded, SyncStatus.Failed, SyncStatus.Skipped);
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

    private async Task<string> LoginAsync(string email, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var tokens = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        return tokens!.AccessToken;
    }

    private async Task<(Guid repoId, Guid userId)> CreateRepoWithTagAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conn = new Domain.Entities.GitProviderConnection
        {
            Id = Guid.NewGuid(),
            Name = "bg-conn",
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
            ExternalId = $"ext-bg-{Guid.NewGuid():N}",
            Name = "bg-test-repo",
            DefaultBranch = "main",
            WebUrl = "https://example.com",
            AzureProjectName = "TestProject",
            IsTracked = true,
            LatestTag = tag
        };
        db.Repositories.Add(repo);
        await db.SaveChangesAsync();

        var userId = db.Users.First(u => u.Email == AdminEmail).Id;
        return (repo.Id, userId);
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, string TokenType);
}

// ── Fake git provider ─────────────────────────────────────────────────────────

internal class FakeSyncGitProvider : IGitProvider
{
    public Task<bool> TestConnectionAsync(ProviderConnection conn, CancellationToken ct = default)
        => Task.FromResult(true);

    public Task<IEnumerable<RepoSummary>> ListRepositoriesAsync(ProviderConnection conn, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<RepoSummary>());

    public Task<IEnumerable<TagInfo>> ListTagsAsync(ProviderConnection conn, string repoExternalId, CancellationToken ct = default)
        => Task.FromResult<IEnumerable<TagInfo>>([new TagInfo("v1.0.0", "abc123", DateTimeOffset.UtcNow, "Test")]);

    public Task<DateTimeOffset?> GetCommitDateAsync(ProviderConnection conn, string repoExternalId, string commitSha, CancellationToken ct = default)
        => Task.FromResult<DateTimeOffset?>(null);

    public Task<IEnumerable<CommitInfo>> GetCommitsBetweenAsync(ProviderConnection conn, string repoExternalId, string fromRef, string toRef, DateTimeOffset? fromDate = null, CancellationToken ct = default)
        => Task.FromResult<IEnumerable<CommitInfo>>([
            new CommitInfo("abc123def456001", "feat: login", "Alice", "alice@example.com", DateTimeOffset.UtcNow),
            new CommitInfo("def456abc789002", "fix: auth XSS", "Bob", "bob@example.com", DateTimeOffset.UtcNow.AddMinutes(-5))
        ]);

    public Task<IEnumerable<PullRequestInfo>> GetMergedPullRequestsAsync(ProviderConnection conn, string repoExternalId, DateTime since, CancellationToken ct = default)
        => Task.FromResult(Enumerable.Empty<PullRequestInfo>());
}

internal class FakeSyncGitProviderFactory : IGitProviderFactory
{
    private readonly FakeSyncGitProvider _provider = new();
    public IGitProvider GetProvider(Domain.Enums.ProviderType providerType) => _provider;
}

internal class SyncTestFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var desc = services.SingleOrDefault(d => d.ServiceType == typeof(IGitProviderFactory));
            if (desc is not null) services.Remove(desc);
            services.AddSingleton<IGitProviderFactory, FakeSyncGitProviderFactory>();
        });
    }
}
