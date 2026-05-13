using System.Net.Http.Headers;
using System.Text;
using RepoManager.Application.Confluence;
using RepoManager.Application.Common.Exceptions;

namespace RepoManager.Infrastructure.Confluence;

public class ConfluencePublisher : IConfluencePublisher
{
    private readonly HttpClient _http;

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

    public Task<PublishResult> CreateOrUpdatePageAsync(ConfluenceConnectionDto conn, string spaceKey, string parentPageId, string title, string markdownContent, string? existingPageId, CancellationToken ct = default)
        => throw new NotImplementedException("Implemented in T071.");

    public Task<PublishResult> CreateChecklistPageAsync(ConfluenceConnectionDto conn, string spaceKey, string parentPageId, string title, string checklistTemplate, CancellationToken ct = default)
        => throw new NotImplementedException("Implemented in T071.");

    private static AuthenticationHeaderValue BuildBasicAuth(string username, string apiToken)
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{apiToken}"));
        return new AuthenticationHeaderValue("Basic", credentials);
    }
}
