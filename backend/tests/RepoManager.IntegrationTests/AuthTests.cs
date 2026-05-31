using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace RepoManager.IntegrationTests;

public class AuthTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private const string AdminEmail = "admin@test.com";
    private const string AdminPassword = "Password123!";

    public AuthTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClientWithSetupKey();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Setup_CreatesAdmin_And_Returns409OnSecondCall()
    {
        var payload = new { email = AdminEmail, password = AdminPassword };

        var firstResponse = await _client.PostAsJsonAsync("/api/v1/auth/setup", payload);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var secondResponse = await _client.PostAsJsonAsync("/api/v1/auth/setup", payload);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        await EnsureAdminExistsAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = AdminEmail, password = AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await EnsureAdminExistsAsync();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = AdminEmail, password = "WrongPassword!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_RotatesToken_And_InvalidatesOldRefreshToken()
    {
        await EnsureAdminExistsAsync();

        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = AdminEmail, password = AdminPassword });
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        tokens.Should().NotBeNull();

        // First refresh: should succeed and return a new pair
        var refreshResponse1 = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = tokens!.RefreshToken });
        refreshResponse1.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = await refreshResponse1.Content.ReadFromJsonAsync<TokenResponse>();
        newTokens.Should().NotBeNull();
        newTokens!.RefreshToken.Should().NotBe(tokens.RefreshToken);

        // Second refresh with the OLD token: should fail (rotated out)
        var refreshResponse2 = await _client.PostAsJsonAsync("/api/v1/auth/refresh",
            new { refreshToken = tokens.RefreshToken });
        refreshResponse2.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.Unauthorized);
    }

    private async Task EnsureAdminExistsAsync()
    {
        var payload = new { email = AdminEmail, password = AdminPassword };
        var response = await _client.PostAsJsonAsync("/api/v1/auth/setup", payload);
        // Accept 201 (created) or 409 (already exists)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, string TokenType);
}
