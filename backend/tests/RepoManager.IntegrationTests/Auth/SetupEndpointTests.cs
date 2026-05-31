using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace RepoManager.IntegrationTests.Auth;

public class SetupEndpointTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private const string AdminEmail = "setup-test-admin@test.com";
    private const string AdminPassword = "Password123!";

    public SetupEndpointTests()
    {
        _factory = new TestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Setup_MissingSetupKeyHeader_Returns401WithSetupKeyInvalidCode()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/setup",
            new { email = AdminEmail, password = AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("setup_key_invalid");
    }

    [Fact]
    public async Task Setup_WrongSetupKeyHeader_Returns401WithSetupKeyInvalidCode()
    {
        _client.DefaultRequestHeaders.Add("X-Setup-Key", "definitely-the-wrong-key-value-here-xx");

        var response = await _client.PostAsJsonAsync("/api/v1/auth/setup",
            new { email = AdminEmail, password = AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("setup_key_invalid");
    }

    [Fact]
    public async Task Setup_CorrectKeyAndEmptyDb_Returns201WithCreatedAdmin()
    {
        _client.DefaultRequestHeaders.Add("X-Setup-Key", TestWebApplicationFactory.TestSetupKey);

        var response = await _client.PostAsJsonAsync("/api/v1/auth/setup",
            new { email = AdminEmail, password = AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("email").GetString().Should().Be(AdminEmail);
    }

    [Fact]
    public async Task Setup_CorrectKeyButUserAlreadyExists_Returns409WithSetupAlreadyCompleteCode()
    {
        _client.DefaultRequestHeaders.Add("X-Setup-Key", TestWebApplicationFactory.TestSetupKey);

        await _client.PostAsJsonAsync("/api/v1/auth/setup",
            new { email = AdminEmail, password = AdminPassword });

        var response = await _client.PostAsJsonAsync("/api/v1/auth/setup",
            new { email = "second-admin@test.com", password = AdminPassword });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("setup_already_complete");
    }
}
