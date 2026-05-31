using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Application.GitProviders;
using RepoManager.Domain.ValueObjects;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests.Api;

public class RepositoryLatestTagTests : IDisposable
{
    private readonly TagTestFactory _factory;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _viewerClient;

    private const string AdminEmail = "tag-admin@test.com";
    private const string AdminPassword = "Password123!";
    private const string ViewerEmail = "tag-viewer@test.com";
    private const string ViewerPassword = "Password123!";

    public RepositoryLatestTagTests()
    {
        _factory = new TagTestFactory();
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
        admin.Role = Domain.Enums.Role.Admin;
        db.Users.Add(new Domain.Entities.User
        {
            Id = Guid.NewGuid(),
            Email = ViewerEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(ViewerPassword),
            Role = Domain.Enums.Role.Viewer,
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

    private async Task<(Guid id, Guid connectionId)> CreateTrackedRepoAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conn = new Domain.Entities.GitProviderConnection
        {
            Id = Guid.NewGuid(),
            Name = "test-conn",
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
            ExternalId = "ext-tag-test",
            Name = "tag-test-repo",
            DefaultBranch = "main",
            WebUrl = "https://example.com",
            AzureProjectName = "TestProject",
            IsTracked = true
        };
        db.Repositories.Add(repo);
        await db.SaveChangesAsync();
        return (repo.Id, conn.Id);
    }

    private async Task<Guid> CreateUntrackedRepoAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conn = new Domain.Entities.GitProviderConnection
        {
            Id = Guid.NewGuid(),
            Name = "untracked-conn",
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
            ExternalId = "ext-untracked",
            Name = "untracked-repo",
            DefaultBranch = "main",
            WebUrl = "https://example.com",
            AzureProjectName = "TestProject",
            IsTracked = false
        };
        db.Repositories.Add(repo);
        await db.SaveChangesAsync();
        return repo.Id;
    }

    // ── tests ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Admin_GetTags_Returns200()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (repoId, _) = await CreateTrackedRepoAsync();

        var resp = await _adminClient.GetAsync($"/api/v1/repositories/{repoId}/tags");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<TagListResponse>();
        body!.Tags.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Viewer_GetTags_Returns200()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_viewerClient, ViewerEmail, ViewerPassword);
        _viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (repoId, _) = await CreateTrackedRepoAsync();

        var resp = await _viewerClient.GetAsync($"/api/v1/repositories/{repoId}/tags");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admin_PinTag_Returns200_WithUpdatedDto()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (repoId, _) = await CreateTrackedRepoAsync();

        var resp = await _adminClient.PutAsJsonAsync(
            $"/api/v1/repositories/{repoId}/latest-tag",
            new { tagName = "v1.0.0" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<RepositoryDtoResponse>();
        dto!.LatestTag.Should().Be("v1.0.0");
    }

    [Fact]
    public async Task Admin_ClearTag_Returns204()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (repoId, _) = await CreateTrackedRepoAsync();

        var resp = await _adminClient.DeleteAsync($"/api/v1/repositories/{repoId}/latest-tag");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Viewer_PinTag_Returns403()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_viewerClient, ViewerEmail, ViewerPassword);
        _viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (repoId, _) = await CreateTrackedRepoAsync();

        var resp = await _viewerClient.PutAsJsonAsync(
            $"/api/v1/repositories/{repoId}/latest-tag",
            new { tagName = "v1.0.0" });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_ClearTag_Returns403()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_viewerClient, ViewerEmail, ViewerPassword);
        _viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (repoId, _) = await CreateTrackedRepoAsync();

        var resp = await _viewerClient.DeleteAsync($"/api/v1/repositories/{repoId}/latest-tag");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PinTag_NonExistentTagName_Returns422()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (repoId, _) = await CreateTrackedRepoAsync();

        var resp = await _adminClient.PutAsJsonAsync(
            $"/api/v1/repositories/{repoId}/latest-tag",
            new { tagName = "v999.999.999-does-not-exist" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetTags_UntrackedRepo_Returns422()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var repoId = await CreateUntrackedRepoAsync();

        var resp = await _adminClient.GetAsync($"/api/v1/repositories/{repoId}/tags");

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task PinTag_UntrackedRepo_Returns422()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var repoId = await CreateUntrackedRepoAsync();

        var resp = await _adminClient.PutAsJsonAsync(
            $"/api/v1/repositories/{repoId}/latest-tag",
            new { tagName = "v1.0.0" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task GetTags_UnknownRepo_Returns404()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _adminClient.GetAsync($"/api/v1/repositories/{Guid.NewGuid()}/tags");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── response types ────────────────────────────────────────────────────────

    private sealed record TokenResponse(string AccessToken, string RefreshToken, string TokenType);

    private sealed record TagListResponse(IReadOnlyList<TagItem> Tags);

    private sealed record TagItem(string Name, string CommitSha, string? CommitDate, string? AuthorName);

    private sealed record RepositoryDtoResponse(
        string Id, string Name, string? LatestTag, string? LatestTagCommitSha, string? LatestTagSetAt);
}

// ── fake git provider service for tests ──────────────────────────────────────

internal class FakeGitProviderService : IGitProviderService
{
    public static readonly IReadOnlyList<RepositoryTag> DefaultTags =
    [
        new("v1.0.0", "abc1234567890abcdef", DateTimeOffset.UtcNow.AddDays(-14), "Jane Smith"),
        new("v2.0.0", "def9876543210fedcba", DateTimeOffset.UtcNow.AddDays(-3), "John Doe"),
    ];

    public Task<IReadOnlyList<RepositoryTag>> ListTagsAsync(Guid repositoryId, CancellationToken ct = default)
        => Task.FromResult(DefaultTags);
}

internal class TagTestFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IGitProviderService));
            if (descriptor is not null)
                services.Remove(descriptor);
            services.AddScoped<IGitProviderService, FakeGitProviderService>();
        });
    }
}
