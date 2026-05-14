namespace RepoManager.Application.Confluence;

public interface IConfluencePublisher
{
    Task<bool> TestConnectionAsync(ConfluenceConnectionDto conn, CancellationToken ct = default);
    Task<PublishResult> CreateOrUpdatePageAsync(ConfluenceConnectionDto conn, string spaceKey, string parentPageId, string title, string markdownContent, string? existingPageId, CancellationToken ct = default);
    Task<PublishResult> CreateChecklistPageAsync(ConfluenceConnectionDto conn, string spaceKey, string parentPageId, string title, string checklistTemplate, CancellationToken ct = default);
}

public record PublishResult(bool Success, string? PageId, string? PageUrl, string? ErrorMessage);
public record ConfluenceConnectionDto(string BaseUrl, string Username, string DecryptedApiToken);
