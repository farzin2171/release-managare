namespace RepoManager.Application.DTOs.Bindings;

public record ProjectTemplateBindingDto(
    Guid Id,
    Guid ProjectId,
    Guid TemplateId,
    string TemplateName,
    string Kind,
    string PageTitleTemplate,
    string? ParentPageId,
    bool LinkFromReleaseNotes,
    int SortOrder);

public record CreateBindingRequest(
    Guid TemplateId,
    string Kind,
    string PageTitleTemplate,
    string? ParentPageId,
    bool LinkFromReleaseNotes,
    int SortOrder);

public record UpdateBindingRequest(
    Guid? TemplateId,
    string? Kind,
    string? PageTitleTemplate,
    string? ParentPageId,
    bool? LinkFromReleaseNotes,
    int? SortOrder);
