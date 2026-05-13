using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.DataProtection;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Jira;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Jira;

public class JiraService : IJiraService
{
    private readonly HttpClient _httpClient;
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public JiraService(HttpClient httpClient, AppDbContext db, IDataProtectionProvider dataProtection)
    {
        _httpClient = httpClient;
        _db = db;
        _protector = dataProtection.CreateProtector("JiraConnection.ApiToken");
    }

    public async Task<bool> TestConnectionAsync(JiraConnectionDto conn, CancellationToken ct = default)
    {
        using var request = BuildRequest(HttpMethod.Get, conn.BaseUrl, "/rest/api/3/myself", conn.Username, conn.DecryptedApiToken);
        var response = await _httpClient.SendAsync(request, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<JiraProjectDto>> ListProjectsAsync(Guid connectionId, CancellationToken ct = default)
    {
        var conn = await ResolveConnectionAsync(connectionId, ct);
        using var request = BuildRequest(HttpMethod.Get, conn.BaseUrl, "/rest/api/3/project/search?maxResults=200&orderBy=name", conn.Username, conn.DecryptedApiToken);
        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new ExternalServiceException("Jira", $"ListProjects failed: {response.StatusCode}", null);
        var body = await response.Content.ReadFromJsonAsync<JiraProjectsResponse>(JsonOptions, ct);
        return body?.Values?.Select(p => new JiraProjectDto(p.Key, p.Name, p.ProjectTypeKey ?? "software")).ToList() ?? [];
    }

    public async Task<JiraReleaseDto> SyncFixVersionAsync(Guid connectionId, string projectKey, string versionName, bool createIfMissing, CancellationToken ct = default)
    {
        var conn = await ResolveConnectionAsync(connectionId, ct);
        var version = await FindOrCreateVersionAsync(conn, projectKey, versionName, createIfMissing, ct);
        var tickets = await FetchVersionTicketsAsync(conn, projectKey, versionName, ct);
        var releaseDate = version.ReleaseDate is not null ? DateOnly.Parse(version.ReleaseDate) : (DateOnly?)null;
        return new JiraReleaseDto(version.Id, version.Name, version.Released, releaseDate, tickets.Select(ToTicketDto).ToList());
    }

    public async Task AddTicketToFixVersionAsync(Guid connectionId, string ticketKey, string versionId, CancellationToken ct = default)
    {
        var conn = await ResolveConnectionAsync(connectionId, ct);
        var payload = JsonSerializer.Serialize(new { update = new { fixVersions = new[] { new { add = new { id = versionId } } } } });
        using var request = BuildRequest(HttpMethod.Put, conn.BaseUrl, $"/rest/api/3/issue/{ticketKey}", conn.Username, conn.DecryptedApiToken);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new ExternalServiceException("Jira", $"AddTicketToFixVersion failed for {ticketKey}: {(int)response.StatusCode}", null);
    }

    private async Task<JiraConnectionDto> ResolveConnectionAsync(Guid connectionId, CancellationToken ct)
    {
        var entity = await _db.JiraConnections.FindAsync([connectionId], ct)
            ?? throw new NotFoundException("JiraConnection", connectionId);
        return new JiraConnectionDto(entity.BaseUrl, entity.Username, _protector.Unprotect(entity.EncryptedApiToken));
    }

    private async Task<JiraVersionInfo> FindOrCreateVersionAsync(JiraConnectionDto conn, string projectKey, string versionName, bool createIfMissing, CancellationToken ct)
    {
        using var listReq = BuildRequest(HttpMethod.Get, conn.BaseUrl, $"/rest/api/3/project/{projectKey}/versions", conn.Username, conn.DecryptedApiToken);
        var listResp = await _httpClient.SendAsync(listReq, ct);
        if (!listResp.IsSuccessStatusCode)
            throw new ExternalServiceException("Jira", $"ListVersions failed for {projectKey}: {(int)listResp.StatusCode}", null);
        var versions = await listResp.Content.ReadFromJsonAsync<List<JiraVersionInfo>>(JsonOptions, ct) ?? [];
        var existing = versions.FirstOrDefault(v => v.Name == versionName);
        if (existing is not null) return existing;
        if (!createIfMissing) throw new NotFoundException("JiraFixVersion", versionName);
        return await CreateVersionAsync(conn, projectKey, versionName, ct);
    }

    private async Task<JiraVersionInfo> CreateVersionAsync(JiraConnectionDto conn, string projectKey, string versionName, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { name = versionName, project = projectKey, released = false });
        using var request = BuildRequest(HttpMethod.Post, conn.BaseUrl, "/rest/api/3/version", conn.Username, conn.DecryptedApiToken);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new ExternalServiceException("Jira", $"CreateVersion failed for {versionName}: {(int)response.StatusCode}", null);
        return await response.Content.ReadFromJsonAsync<JiraVersionInfo>(JsonOptions, ct)
            ?? throw new ExternalServiceException("Jira", "CreateVersion returned an empty response.", null);
    }

    private async Task<List<JiraIssueInfo>> FetchVersionTicketsAsync(JiraConnectionDto conn, string projectKey, string versionName, CancellationToken ct)
    {
        var jql = Uri.EscapeDataString($"project = {projectKey} AND fixVersion = \"{versionName}\"");
        var path = $"/rest/api/3/search?jql={jql}&maxResults=500&fields=summary,status,issuetype,assignee,priority,parent";
        using var request = BuildRequest(HttpMethod.Get, conn.BaseUrl, path, conn.Username, conn.DecryptedApiToken);
        var response = await _httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new ExternalServiceException("Jira", $"SearchIssues failed: {(int)response.StatusCode}", null);
        var body = await response.Content.ReadFromJsonAsync<JiraSearchResponse>(JsonOptions, ct);
        return body?.Issues ?? [];
    }

    private static JiraTicketDto ToTicketDto(JiraIssueInfo issue)
    {
        var category = issue.Fields?.Status?.StatusCategory?.Key switch
        {
            "done" => JiraStatusCategory.Done,
            "indeterminate" => JiraStatusCategory.InProgress,
            _ => JiraStatusCategory.ToDo
        };
        return new JiraTicketDto(
            issue.Key,
            issue.Fields?.Summary ?? string.Empty,
            issue.Fields?.Status?.Name ?? string.Empty,
            category,
            issue.Fields?.Issuetype?.Name ?? string.Empty,
            issue.Fields?.Assignee?.DisplayName,
            issue.Fields?.Assignee?.EmailAddress,
            issue.Fields?.Priority?.Name,
            issue.Fields?.Parent?.Key);
    }

    private static HttpRequestMessage BuildRequest(HttpMethod method, string baseUrl, string path, string username, string apiToken)
    {
        var request = new HttpRequestMessage(method, baseUrl.TrimEnd('/') + path);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{apiToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Headers.Add("Accept", "application/json");
        return request;
    }

    private record JiraProjectsResponse([property: JsonPropertyName("values")] List<JiraProjectInfo>? Values);
    private record JiraProjectInfo(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("projectTypeKey")] string? ProjectTypeKey);
    private record JiraVersionInfo(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("released")] bool Released,
        [property: JsonPropertyName("releaseDate")] string? ReleaseDate);
    private record JiraSearchResponse([property: JsonPropertyName("issues")] List<JiraIssueInfo>? Issues);
    private record JiraIssueInfo(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("fields")] JiraIssueFields? Fields);
    private record JiraIssueFields(
        [property: JsonPropertyName("summary")] string? Summary,
        [property: JsonPropertyName("status")] JiraIssueStatus? Status,
        [property: JsonPropertyName("issuetype")] JiraIssueType? Issuetype,
        [property: JsonPropertyName("assignee")] JiraAssignee? Assignee,
        [property: JsonPropertyName("priority")] JiraPriority? Priority,
        [property: JsonPropertyName("parent")] JiraParent? Parent);
    private record JiraIssueStatus(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("statusCategory")] JiraStatusCategoryInfo? StatusCategory);
    private record JiraStatusCategoryInfo([property: JsonPropertyName("key")] string? Key);
    private record JiraIssueType([property: JsonPropertyName("name")] string? Name);
    private record JiraAssignee(
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("emailAddress")] string? EmailAddress);
    private record JiraPriority([property: JsonPropertyName("name")] string? Name);
    private record JiraParent([property: JsonPropertyName("key")] string? Key);
}
