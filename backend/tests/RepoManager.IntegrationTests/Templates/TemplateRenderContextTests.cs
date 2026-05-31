using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.IntegrationTests.Templates;

public class TemplateRenderContextTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _adminClient;

    private const string AdminEmail = "rct-admin@test.com";
    private const string AdminPassword = "Password123!";

    public TemplateRenderContextTests()
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
        admin.Role = Role.Admin;
        await db.SaveChangesAsync();
    }

    private async Task<string> LoginAsync()
    {
        var resp = await _adminClient.PostAsJsonAsync("/api/v1/auth/login",
            new { email = AdminEmail, password = AdminPassword });
        var tokens = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        return tokens!.AccessToken;
    }

    private async Task<(Guid projectId, Guid templateId)> SeedProjectWithTwoReposAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var connId = Guid.NewGuid();
        db.GitProviderConnections.Add(new GitProviderConnection
        {
            Id = connId,
            Name = "rct-conn",
            ProviderType = ProviderType.AzureDevOps,
            OrganizationUrl = "https://dev.azure.com/test",
            EncryptedPat = "dW5lbmNyeXB0ZWQ=",
            IsActive = true
        });

        var repo1Id = Guid.NewGuid();
        var repo2Id = Guid.NewGuid();
        db.Repositories.AddRange(
            new Repository
            {
                Id = repo1Id,
                GitProviderConnectionId = connId,
                ExternalId = "rct-r1",
                Name = "api-service",
                DefaultBranch = "main",
                WebUrl = "https://example.com",
                AzureProjectName = "TestProject",
                ServiceOwner = "Platform Team",
                IsTracked = true
            },
            new Repository
            {
                Id = repo2Id,
                GitProviderConnectionId = connId,
                ExternalId = "rct-r2",
                Name = "frontend-app",
                DefaultBranch = "main",
                WebUrl = "https://example.com",
                AzureProjectName = "TestProject",
                ServiceOwner = null,
                IsTracked = true
            });

        var projectId = Guid.NewGuid();
        db.Projects.Add(new Project
        {
            Id = projectId,
            Name = "RCT-Project",
            Color = "#3B82F6",
            JiraProjectKeys = "[]",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        db.ProjectRepositories.AddRange(
            new ProjectRepository { ProjectId = projectId, RepositoryId = repo1Id, IsPrimary = true },
            new ProjectRepository { ProjectId = projectId, RepositoryId = repo2Id });

        var userId = db.Users.First().Id;
        var releaseId = Guid.NewGuid();
        db.Releases.Add(new Release
        {
            Id = releaseId,
            ProjectId = projectId,
            Version = "1.0.0",
            Status = ReleaseStatus.Draft,
            GeneratedNotesMarkdown = "",
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow
        });
        db.ReleaseRepositories.AddRange(
            new ReleaseRepository
            {
                ReleaseId = releaseId,
                RepositoryId = repo1Id,
                PreviousVersion = "0.9.0",
                NextVersion = "1.0.0",
                CommitCount = 5,
                TicketCount = 2
            },
            new ReleaseRepository
            {
                ReleaseId = releaseId,
                RepositoryId = repo2Id,
                PreviousVersion = "2.3.0",
                NextVersion = "2.4.0",
                CommitCount = 3,
                TicketCount = 1
            });

        var templateId = Guid.NewGuid();
        db.ReleaseNoteTemplates.Add(new ReleaseNoteTemplate
        {
            Id = templateId,
            Name = "RCT-Test-Template",
            ContentTemplate = "{{#each repositories}}<row>{{name}}|{{serviceOwner}}</row>{{/each}}",
            IsDefault = false,
            IsSystem = false
        });

        await db.SaveChangesAsync();
        return (projectId, templateId);
    }

    [Fact]
    public async Task PreviewTemplate_TwoReposWithServiceOwner_RendersTableWithBothRows()
    {
        await SetupAdminAsync();
        var token = await LoginAsync();
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var (projectId, templateId) = await SeedProjectWithTwoReposAsync();

        var resp = await _adminClient.GetAsync(
            $"/api/v1/templates/{templateId}/preview?contextSource=project&projectId={projectId}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await resp.Content.ReadFromJsonAsync<PreviewResponse>();
        preview!.RenderedBody.Should().Contain("<row>api-service|Platform Team</row>");
        preview.RenderedBody.Should().Contain("<row>frontend-app|</row>");
    }

    [Fact]
    public async Task PreviewTemplate_SyntheticContext_RendersRepositoriesBlock()
    {
        await SetupAdminAsync();
        var token = await LoginAsync();
        _adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Use the seeded system template with synthetic context
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var systemTemplate = db.ReleaseNoteTemplates.First(t => t.IsSystem);

        var resp = await _adminClient.GetAsync(
            $"/api/v1/templates/{systemTemplate.Id}/preview?contextSource=synthetic");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var preview = await resp.Content.ReadFromJsonAsync<PreviewResponse>();
        preview!.RenderedBody.Should().Contain("<td><strong>sample-api</strong></td>");
    }

    private sealed record TokenResponse(string AccessToken, string RefreshToken, string TokenType);
    private sealed record PreviewResponse(string RenderedTitle, string RenderedBody, List<string> UnknownTokens);
}
