using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests.Templates;

public class TemplateSystemFlagTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _adminClient;

    private const string AdminEmail = "tsf-admin@test.com";
    private const string AdminPassword = "Password123!";

    public TemplateSystemFlagTests()
    {
        _factory = new TestWebApplicationFactory();
        _adminClient = _factory.CreateClientWithSetupKey();
    }

    public void Dispose()
    {
        _adminClient.Dispose();
        _factory.Dispose();
    }

    private async Task SetupAdminAsync()
    {
        await _adminClient.PostAsJsonAsync("/api/v1/auth/setup",
            new { email = AdminEmail, password = AdminPassword });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = db.Users.Single(u => u.Email == AdminEmail);
        admin.Role = Domain.Enums.Role.Admin;
        await db.SaveChangesAsync();
    }

    private async Task<string> LoginAsync()
    {
        var resp = await _adminClient.PostAsJsonAsync("/api/v1/auth/login",
            new { email = AdminEmail, password = AdminPassword });
        var tokens = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        return tokens!.AccessToken;
    }

    private async Task<Guid> GetSystemTemplateIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var t = db.ReleaseNoteTemplates.Single(t => t.IsSystem);
        return t.Id;
    }

    [Fact]
    public async Task GetAllTemplates_ReturnsSystemTemplateWithIsSystemTrue()
    {
        await SetupAdminAsync();
        var token = await LoginAsync();
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _adminClient.GetAsync("/api/v1/templates");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var templates = await resp.Content.ReadFromJsonAsync<List<TemplateSummary>>();
        templates.Should().Contain(t => t.IsSystem);
    }

    [Fact]
    public async Task UpdateSystemTemplate_Returns403WithCode()
    {
        await SetupAdminAsync();
        var token = await LoginAsync();
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var systemId = await GetSystemTemplateIdAsync();

        var resp = await _adminClient.PutAsJsonAsync(
            $"/api/v1/templates/{systemId}",
            new { name = "Hacked Name" });

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("system_template_readonly");
    }

    [Fact]
    public async Task DeleteSystemTemplate_Returns403WithCode()
    {
        await SetupAdminAsync();
        var token = await LoginAsync();
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var systemId = await GetSystemTemplateIdAsync();

        var resp = await _adminClient.DeleteAsync($"/api/v1/templates/{systemId}");

        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("system_template_readonly");
    }

    [Fact]
    public async Task CloneSystemTemplate_Returns201EditableCopy()
    {
        await SetupAdminAsync();
        var token = await LoginAsync();
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var systemId = await GetSystemTemplateIdAsync();

        var resp = await _adminClient.PostAsync($"/api/v1/templates/{systemId}/clone", null);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var clone = await resp.Content.ReadFromJsonAsync<TemplateSummary>();
        clone!.IsSystem.Should().BeFalse();
        clone.Name.Should().Contain("(copy)");
    }

    [Fact]
    public async Task CloneTwice_ProducesAutoIncrementedName()
    {
        await SetupAdminAsync();
        var token = await LoginAsync();
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var systemId = await GetSystemTemplateIdAsync();

        var resp1 = await _adminClient.PostAsync($"/api/v1/templates/{systemId}/clone", null);
        var resp2 = await _adminClient.PostAsync($"/api/v1/templates/{systemId}/clone", null);

        resp1.StatusCode.Should().Be(HttpStatusCode.Created);
        resp2.StatusCode.Should().Be(HttpStatusCode.Created);

        var clone1 = await resp1.Content.ReadFromJsonAsync<TemplateSummary>();
        var clone2 = await resp2.Content.ReadFromJsonAsync<TemplateSummary>();

        clone1!.Name.Should().NotBe(clone2!.Name);
        clone2.Name.Should().Contain("(copy) 2");
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, string TokenType);
    private sealed record TemplateSummary(Guid Id, string Name, bool IsSystem);
}
