using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests.Repositories;

public class ServiceOwnerTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _viewerClient;

    private const string AdminEmail = "so-admin@test.com";
    private const string AdminPassword = "Password123!";
    private const string ViewerEmail = "so-viewer@test.com";
    private const string ViewerPassword = "Password123!";

    public ServiceOwnerTests()
    {
        _factory = new TestWebApplicationFactory();
        _adminClient = _factory.CreateClientWithSetupKey();
        _viewerClient = _factory.CreateClient();
    }

    public void Dispose()
    {
        _adminClient.Dispose();
        _viewerClient.Dispose();
        _factory.Dispose();
    }

    private async Task SetupUsersAsync()
    {
        await _adminClient.PostAsJsonAsync("/api/v1/auth/setup",
            new { email = AdminEmail, password = AdminPassword });

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

    private async Task<Guid> CreateRepositoryAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var conn = new Domain.Entities.GitProviderConnection
        {
            Id = Guid.NewGuid(),
            Name = "so-conn",
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
            ExternalId = "ext-so-test",
            Name = "so-test-repo",
            DefaultBranch = "main",
            WebUrl = "https://example.com",
            AzureProjectName = "TestProject",
            IsTracked = true
        };
        db.Repositories.Add(repo);
        await db.SaveChangesAsync();
        return repo.Id;
    }

    [Fact]
    public async Task SetServiceOwner_ViaAdmin_Put_ThenGet_ReturnsValue()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var repoId = await CreateRepositoryAsync();

        var putResp = await _adminClient.PutAsJsonAsync(
            $"/api/v1/repositories/{repoId}",
            new { serviceOwner = "Platform Team" });

        putResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await putResp.Content.ReadFromJsonAsync<RepositoryResponse>();
        dto!.ServiceOwner.Should().Be("Platform Team");
    }

    [Fact]
    public async Task ClearServiceOwner_ViaAdmin_Put_NullValue_GetReturnsNull()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var repoId = await CreateRepositoryAsync();

        await _adminClient.PutAsJsonAsync(
            $"/api/v1/repositories/{repoId}",
            new { serviceOwner = "Platform Team" });

        var putResp = await _adminClient.PutAsJsonAsync(
            $"/api/v1/repositories/{repoId}",
            new { serviceOwner = (string?)null });

        putResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await putResp.Content.ReadFromJsonAsync<RepositoryResponse>();
        dto!.ServiceOwner.Should().BeNull();
    }

    [Fact]
    public async Task SetServiceOwner_121Chars_Returns422()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_adminClient, AdminEmail, AdminPassword);
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var repoId = await CreateRepositoryAsync();

        var tooLong = new string('x', 121);
        var putResp = await _adminClient.PutAsJsonAsync(
            $"/api/v1/repositories/{repoId}",
            new { serviceOwner = tooLong });

        putResp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task SetServiceOwner_AsViewer_Returns403()
    {
        await SetupUsersAsync();
        var token = await LoginAsync(_viewerClient, ViewerEmail, ViewerPassword);
        _viewerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var repoId = await CreateRepositoryAsync();

        var putResp = await _viewerClient.PutAsJsonAsync(
            $"/api/v1/repositories/{repoId}",
            new { serviceOwner = "Platform Team" });

        putResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, string TokenType);

    private sealed record RepositoryResponse(
        string Id,
        string Name,
        string? ServiceOwner);
}
