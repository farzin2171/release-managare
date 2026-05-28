namespace RepoManager.Application.DTOs.Releases;

public record PreparedPageDto(
    Guid BindingId,
    string Kind,
    string Title,
    string Body,
    string? ParentPageId,
    bool LinkFromReleaseNotes,
    int SortOrder,
    IReadOnlyList<string> UnknownTokens);

public record PreparedReleaseDto(
    ReleaseRenderContextDto Context,
    IReadOnlyList<PreparedPageDto> Pages,
    IReadOnlyList<string> Warnings);

public record PreparePageRequest(
    string? AdminOverrideVersion,
    ReconciliationSummaryDto? ReconciliationData);

public record PublishPagesRequest(IReadOnlyList<PublishPageDto> Pages);

public record PublishPageDto(
    Guid BindingId,
    string Title,
    string Body,
    string? ParentPageId,
    bool LinkFromReleaseNotes,
    int SortOrder,
    string? ExistingConfluencePageId = null);

public record PublishResultDto(IReadOnlyList<PublishedPageDto> PublishedPages);

public record PublishedPageDto(
    Guid BindingId,
    string ConfluencePageId,
    string ConfluenceUrl,
    string Title);

public record TemplatePreviewRequest(string ContextSource, Guid? ProjectId);

public record TemplatePreviewDto(
    string RenderedTitle,
    string RenderedBody,
    IReadOnlyList<string> UnknownTokens,
    string ContextSource,
    string? ProjectName,
    string? ReleaseVersion);
