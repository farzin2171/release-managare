using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Confluence;

namespace RepoManager.Infrastructure.Confluence;

public class ConfluencePublisher : IConfluencePublisher
{
    private readonly HttpClient _http;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public ConfluencePublisher(HttpClient http) => _http = http;

    public async Task<bool> TestConnectionAsync(ConfluenceConnectionDto conn, CancellationToken ct = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{conn.BaseUrl.TrimEnd('/')}/wiki/api/v2/spaces?limit=1");
            request.Headers.Authorization = BuildBasicAuth(conn.Username, conn.DecryptedApiToken);
            using var response = await _http.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<PublishResult> CreateOrUpdatePageAsync(
        ConfluenceConnectionDto conn,
        string spaceKey,
        string parentPageId,
        string title,
        string markdownContent,
        string? existingPageId,
        CancellationToken ct = default)
    {
        try
        {
            var storageContent = MarkdownToConfluenceConverter.Convert(markdownContent);
            if (existingPageId is not null)
                return await UpdatePageAsync(conn, existingPageId, title, storageContent, ct);

            var spaceId = await ResolveSpaceIdAsync(conn, spaceKey, ct);
            return await CreatePageAsync(conn, spaceId, parentPageId, title, storageContent, ct);
        }
        catch (ExternalServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new PublishResult(false, null, null, ex.Message);
        }
    }

    public async Task<PublishResult> CreateChecklistPageAsync(
        ConfluenceConnectionDto conn,
        string spaceKey,
        string parentPageId,
        string title,
        string checklistTemplate,
        CancellationToken ct = default)
    {
        try
        {
            var spaceId = await ResolveSpaceIdAsync(conn, spaceKey, ct);
            return await CreatePageAsync(conn, spaceId, parentPageId, title, checklistTemplate, ct);
        }
        catch (ExternalServiceException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new PublishResult(false, null, null, ex.Message);
        }
    }

    private async Task<string> ResolveSpaceIdAsync(ConfluenceConnectionDto conn, string spaceKey, CancellationToken ct)
    {
        var url = $"{conn.BaseUrl.TrimEnd('/')}/wiki/api/v2/spaces?keys={Uri.EscapeDataString(spaceKey)}&limit=1";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = BuildBasicAuth(conn.Username, conn.DecryptedApiToken);
        request.Headers.Add("Accept", "application/json");
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new ExternalServiceException("Confluence", $"Failed to resolve space key '{spaceKey}': {(int)response.StatusCode}", null);
        var body = await response.Content.ReadFromJsonAsync<ConfluenceSpacesResponse>(JsonOptions, ct);
        return body?.Results?.FirstOrDefault()?.Id
            ?? throw new ExternalServiceException("Confluence", $"Space with key '{spaceKey}' not found.", null);
    }

    private async Task<PublishResult> CreatePageAsync(
        ConfluenceConnectionDto conn, string spaceId, string parentPageId,
        string title, string storageContent, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            spaceId,
            parentId = parentPageId,
            status = "current",
            title,
            body = new { representation = "storage", value = storageContent }
        });
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"{conn.BaseUrl.TrimEnd('/')}/wiki/api/v2/pages");
        request.Headers.Authorization = BuildBasicAuth(conn.Username, conn.DecryptedApiToken);
        request.Headers.Add("Accept", "application/json");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new ExternalServiceException("Confluence", $"CreatePage failed: {(int)response.StatusCode}", null);
        var result = await response.Content.ReadFromJsonAsync<ConfluencePageResponse>(JsonOptions, ct)
            ?? throw new ExternalServiceException("Confluence", "CreatePage returned an empty response.", null);
        return new PublishResult(true, result.Id, BuildPageUrl(conn.BaseUrl, result.Links?.WebUi), null);
    }

    private async Task<PublishResult> UpdatePageAsync(
        ConfluenceConnectionDto conn, string pageId,
        string title, string storageContent, CancellationToken ct)
    {
        using var getReq = new HttpRequestMessage(HttpMethod.Get,
            $"{conn.BaseUrl.TrimEnd('/')}/wiki/api/v2/pages/{pageId}");
        getReq.Headers.Authorization = BuildBasicAuth(conn.Username, conn.DecryptedApiToken);
        getReq.Headers.Add("Accept", "application/json");
        using var getResp = await _http.SendAsync(getReq, ct);
        if (!getResp.IsSuccessStatusCode)
            throw new ExternalServiceException("Confluence", $"GetPage failed for '{pageId}': {(int)getResp.StatusCode}", null);
        var existing = await getResp.Content.ReadFromJsonAsync<ConfluencePageResponse>(JsonOptions, ct)
            ?? throw new ExternalServiceException("Confluence", $"GetPage '{pageId}' returned an empty response.", null);

        var newVersion = (existing.Version?.Number ?? 1) + 1;
        var payload = JsonSerializer.Serialize(new
        {
            id = pageId,
            status = "current",
            title,
            body = new { representation = "storage", value = storageContent },
            version = new { number = newVersion, message = "Updated by RepoManager" }
        });
        using var putReq = new HttpRequestMessage(HttpMethod.Put,
            $"{conn.BaseUrl.TrimEnd('/')}/wiki/api/v2/pages/{pageId}");
        putReq.Headers.Authorization = BuildBasicAuth(conn.Username, conn.DecryptedApiToken);
        putReq.Headers.Add("Accept", "application/json");
        putReq.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var putResp = await _http.SendAsync(putReq, ct);
        if (!putResp.IsSuccessStatusCode)
            throw new ExternalServiceException("Confluence", $"UpdatePage failed for '{pageId}': {(int)putResp.StatusCode}", null);
        var result = await putResp.Content.ReadFromJsonAsync<ConfluencePageResponse>(JsonOptions, ct)
            ?? throw new ExternalServiceException("Confluence", $"UpdatePage '{pageId}' returned an empty response.", null);
        return new PublishResult(true, result.Id, BuildPageUrl(conn.BaseUrl, result.Links?.WebUi), null);
    }

    private static string BuildPageUrl(string baseUrl, string? webUiPath) =>
        webUiPath is not null ? $"{baseUrl.TrimEnd('/')}{webUiPath}" : string.Empty;

    private static AuthenticationHeaderValue BuildBasicAuth(string username, string apiToken)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{apiToken}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }

    private record ConfluenceSpacesResponse([property: JsonPropertyName("results")] List<ConfluenceSpaceInfo>? Results);
    private record ConfluenceSpaceInfo([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("key")] string Key);
    private record ConfluencePageResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("version")] ConfluencePageVersion? Version,
        [property: JsonPropertyName("_links")] ConfluencePageLinks? Links);
    private record ConfluencePageVersion([property: JsonPropertyName("number")] int Number);
    private record ConfluencePageLinks([property: JsonPropertyName("webui")] string? WebUi);
}
