using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Jira;
using RepoManager.Application.Jira.Dtos;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests.Api;

public class JiraCoverageTests : IDisposable
{
    private readonly JiraCoverageTestFactory _factory;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _viewerClient;

    private const string AdminEmail = "jira-admin@test.com";
    private const string AdminPassword = "Password123!";
    private const string ViewerEmail = "jira-viewer@test.com";
    private const string ViewerPassword = "Password123!";

    public JiraCoverageTests()
    {
        _factory = new JiraCoverageTestFactory();
        _adminClient = _factory.CreateClientWithSetupKey();
        _viewerClient = _factory.CreateClient();
    }

    public void Dispose()
    {
        _adminClient.Dispose();
        _viewerClient.Dispose();
        _factory.Dispose();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task SetupUsersAsync()
    {
        await _adminClient.PostAsJsonAsync("/api/v1/auth/setup", new { email = AdminEmail, password = AdminPassword });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = db.Users.Single(u => u.Email == AdminEmail);
        admin.Role = Role.Admin;
        db.Users.Add(new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = ViewerEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(ViewerPassword),
            Role = Role.Viewer,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var resp = await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var tokens = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        return tokens!.AccessToken;
    }

    private static RepoJiraComparisonDto MakeRepoDto(Guid repoId) =>
        new(repoId, "test-repo", "1.0.0", "1.1.0", "test-repo_1.1.0",
            false, true, null,
            new ComparisonCounts(5, 2, 2, 2, 0, 0),
            1.0m, HealthBand.Green, [], [], [], [], DateTime.UtcNow);

    private static ProjectJiraCoverageDto MakeProjectDto(Guid projectId) =>
        new(projectId, "Test Project", 1, 1, 0, 1.0m, []);

    // ── unauthenticated ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetForRepo_Unauthenticated_Returns401()
    {
        var resp = await _adminClient.GetAsync($"/api/v1/repositories/{Guid.NewGuid()}/jira-coverage");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetForProject_Unauthenticated_Returns401()
    {
        var resp = await _adminClient.GetAsync($"/api/v1/projects/{Guid.NewGuid()}/jira-coverage");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── cache hit (service returns DTO) ───────────────────────────────────────

    [Fact]
    public async Task GetForRepo_CacheHit_Returns200WithDto()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var repoId = Guid.NewGuid();
        _factory.FakeService.OnGetForRepo = (_, _, _) => Task.FromResult(MakeRepoDto(repoId));

        var resp = await _adminClient.GetAsync($"/api/v1/repositories/{repoId}/jira-coverage");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("repositoryId").GetGuid().Should().Be(repoId);
        body.GetProperty("supported").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetForRepo_WithRefreshFlag_Returns200()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var repoId = Guid.NewGuid();
        bool refreshPassed = false;
        _factory.FakeService.OnGetForRepo = (_, refresh, _) =>
        {
            refreshPassed = refresh;
            return Task.FromResult(MakeRepoDto(repoId));
        };

        var resp = await _adminClient.GetAsync($"/api/v1/repositories/{repoId}/jira-coverage?refresh=true");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshPassed.Should().BeTrue();
    }

    // ── cache miss (service throws NotFoundException) → 404 ──────────────────

    [Fact]
    public async Task GetForRepo_UnknownRepo_Returns404()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // FakeService.OnGetForRepo is null → throws NotFoundException

        var resp = await _adminClient.GetAsync($"/api/v1/repositories/{Guid.NewGuid()}/jira-coverage");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetForProject_UnknownProject_Returns404()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // FakeService.OnGetForProject is null → throws NotFoundException

        var resp = await _adminClient.GetAsync($"/api/v1/projects/{Guid.NewGuid()}/jira-coverage");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetForProject_Returns200WithDto()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var projectId = Guid.NewGuid();
        _factory.FakeService.OnGetForProject = (_, _, _) => Task.FromResult(MakeProjectDto(projectId));

        var resp = await _adminClient.GetAsync($"/api/v1/projects/{projectId}/jira-coverage");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("projectId").GetGuid().Should().Be(projectId);
    }

    // ── RBAC: add-ticket ──────────────────────────────────────────────────────

    [Fact]
    public async Task AddTicket_Viewer_Returns403()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_viewerClient, ViewerEmail, ViewerPassword);
        _viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _viewerClient.PostAsJsonAsync(
            $"/api/v1/repositories/{Guid.NewGuid()}/jira-coverage/add-ticket",
            new { ticketKey = "PROJ-123" });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AddTicket_Admin_NoSupportedSnapshot_Returns409()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        _factory.FakeService.OnAddTicket = (_, _, _) =>
            Task.FromException<AddToFixVersionResultDto>(
                new ConflictException("Repository 'test-repo' has no valid SemVer tag; fix version cannot be determined."));

        var resp = await _adminClient.PostAsJsonAsync(
            $"/api/v1/repositories/{Guid.NewGuid()}/jira-coverage/add-ticket",
            new { ticketKey = "PROJ-123" });

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddTicket_Admin_ValidRepo_Returns200()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var repoId = Guid.NewGuid();
        _factory.FakeService.OnAddTicket = (_, _, _) =>
            Task.FromResult(new AddToFixVersionResultDto(true, "test-repo_1.1.0", true));

        var resp = await _adminClient.PostAsJsonAsync(
            $"/api/v1/repositories/{repoId}/jira-coverage/add-ticket",
            new { ticketKey = "PROJ-123" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<AddTicketResultBody>();
        body!.Success.Should().BeTrue();
        body.JiraFixVersionName.Should().Be("test-repo_1.1.0");
        body.FixVersionCreated.Should().BeTrue();
    }

    [Fact]
    public async Task AddTicket_Admin_EmptyTicketKey_Returns400()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _adminClient.PostAsJsonAsync(
            $"/api/v1/repositories/{Guid.NewGuid()}/jira-coverage/add-ticket",
            new { ticketKey = "" });

        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddTicket_Admin_UnknownRepo_Returns404()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // FakeService.OnAddTicket is null → throws NotFoundException

        var resp = await _adminClient.PostAsJsonAsync(
            $"/api/v1/repositories/{Guid.NewGuid()}/jira-coverage/add-ticket",
            new { ticketKey = "PROJ-123" });

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── response types ────────────────────────────────────────────────────────

    private sealed record TokenResponse(string AccessToken, string RefreshToken, string TokenType);
    private sealed record AddTicketResultBody(bool Success, string JiraFixVersionName, bool FixVersionCreated);
}

// ── Stub service ──────────────────────────────────────────────────────────────

internal sealed class StubRepoJiraComparisonService : IRepoJiraComparisonService
{
    public Func<Guid, bool, CancellationToken, Task<RepoJiraComparisonDto>>? OnGetForRepo { get; set; }
    public Func<Guid, bool, CancellationToken, Task<ProjectJiraCoverageDto>>? OnGetForProject { get; set; }
    public Func<Guid, string, CancellationToken, Task<AddToFixVersionResultDto>>? OnAddTicket { get; set; }

    public Task<RepoJiraComparisonDto> GetForRepoAsync(Guid repositoryId, bool forceRefresh, CancellationToken ct = default)
    {
        if (OnGetForRepo is null) throw new NotFoundException("Repository", repositoryId);
        return OnGetForRepo(repositoryId, forceRefresh, ct);
    }

    public Task<ProjectJiraCoverageDto> GetForProjectAsync(Guid projectId, bool forceRefresh, CancellationToken ct = default)
    {
        if (OnGetForProject is null) throw new NotFoundException("Project", projectId);
        return OnGetForProject(projectId, forceRefresh, ct);
    }

    public Task<AddToFixVersionResultDto> AddTicketToFixVersionAsync(Guid repositoryId, string ticketKey, CancellationToken ct = default)
    {
        if (OnAddTicket is null) throw new NotFoundException("Repository", repositoryId);
        return OnAddTicket(repositoryId, ticketKey, ct);
    }
}

// ── Test factory ──────────────────────────────────────────────────────────────

internal sealed class JiraCoverageTestFactory : TestWebApplicationFactory
{
    public StubRepoJiraComparisonService FakeService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var d = services.SingleOrDefault(s => s.ServiceType == typeof(IRepoJiraComparisonService));
            if (d is not null) services.Remove(d);
            services.AddSingleton<IRepoJiraComparisonService>(FakeService);
        });
    }
}
