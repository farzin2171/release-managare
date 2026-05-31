using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Application.DTOs;
using RepoManager.Application.Events;
using RepoManager.Application.GitProviders;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests.Api;

public class ProjectSyncIntegrationTests : IDisposable
{
    private readonly ProjectSyncSseTestFactory _factory;
    private readonly HttpClient _client;

    private const string AdminEmail = "proj-sse-admin@test.com";
    private const string AdminPassword = "Password123!";

    public ProjectSyncIntegrationTests()
    {
        _factory = new ProjectSyncSseTestFactory();
        _client = _factory.CreateClientWithSetupKey();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    // SSE event sequence: repo_started → step_changed(×5) → repo_completed → project_complete ──

    [Fact]
    public async Task ProjectSync_PublishesSseEventSequence()
    {
        var token = await SetupAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var projectId = await CreateProjectWithSingleTaggedRepoAsync("v1.0.0");

        // Enqueue the project sync directly via the service (to get the ID before worker runs)
        using var scope = _factory.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<Application.Services.IProjectSyncService>();
        var userId = await GetAdminUserIdAsync();
        var projectEventPublisher = _factory.Services.GetRequiredService<IProjectSyncEventPublisher>();

        var run = await syncService.EnqueueAsync(projectId, userId);

        var events = new List<ProjectSseMessage>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        try
        {
            await foreach (var msg in projectEventPublisher.SubscribeAsync(run.Id, null, cts.Token))
            {
                events.Add(msg);
                if (msg.Event == "project_complete") break;
            }
        }
        catch (OperationCanceledException) { }

        events.Select(e => e.Event).Should().Contain("repo_started");
        events.Select(e => e.Event).Should().Contain("step_changed");
        events.Select(e => e.Event).Should().Contain("repo_completed");
        events.Select(e => e.Event).Should().Contain("project_complete");
    }

    // Cancel via DELETE endpoint mid-run ────────────────────────────────────

    [Fact]
    public async Task ProjectSync_CancelViaEndpoint_ReturnsCancelledStatus()
    {
        var token = await SetupAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var projectId = await CreateProjectWithSingleTaggedRepoAsync("v2.0.0");

        var resp = await _client.PostAsync($"/api/v1/projects/{projectId}/sync", null);
        resp.StatusCode.Should().Be(HttpStatusCode.Accepted);

        await Task.Delay(100);

        var cancelResp = await _client.DeleteAsync($"/api/v1/projects/{projectId}/sync/active");
        cancelResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await cancelResp.Content.ReadFromJsonAsync<CancelResponse>();
        body!.Message.Should().Contain("Cancellation requested");
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

    private async Task<Guid> CreateProjectWithSingleTaggedRepoAsync(string tag)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conn = new Domain.Entities.GitProviderConnection
        {
            Id = Guid.NewGuid(), Name = $"conn-sse-{Guid.NewGuid():N}",
            ProviderType = ProviderType.AzureDevOps,
            OrganizationUrl = "https://dev.azure.com/test",
            EncryptedPat = "dW5lbmNyeXB0ZWQ=", IsActive = true
        };
        db.GitProviderConnections.Add(conn);

        var project = new Domain.Entities.Project
        {
            Id = Guid.NewGuid(), Name = $"proj-sse-{Guid.NewGuid():N}",
            Color = "#6B7280", JiraProjectKeys = "[]",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Projects.Add(project);

        var repo = new Domain.Entities.Repository
        {
            Id = Guid.NewGuid(), GitProviderConnectionId = conn.Id,
            ExternalId = $"ext-sse-{tag}-{Guid.NewGuid():N}",
            Name = $"repo-sse-{tag}", DefaultBranch = "main",
            WebUrl = "https://example.com", AzureProjectName = "TestProject",
            IsTracked = true, LatestTag = tag
        };
        db.Repositories.Add(repo);
        db.ProjectRepositories.Add(new Domain.Entities.ProjectRepository
        {
            ProjectId = project.Id, RepositoryId = repo.Id
        });

        await db.SaveChangesAsync();
        return project.Id;
    }

    private async Task<Guid> GetAdminUserIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Users.Single(u => u.Email == AdminEmail).Id;
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, string TokenType);
    private sealed record CancelResponse(string Message);
}

// ── SSE test factory ─────────────────────────────────────────────────────────

internal sealed class ProjectSyncSseTestFactory : TestWebApplicationFactory
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
