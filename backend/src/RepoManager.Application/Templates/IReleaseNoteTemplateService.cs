namespace RepoManager.Application.Templates;

public interface IReleaseNoteTemplateService
{
    Task<TemplateDto> CreateAsync(CreateTemplateDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateDto>> ListAsync(CancellationToken ct = default);
    Task<TemplateDto> UpdateAsync(Guid id, UpdateTemplateDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<TemplateDto> CloneAsync(Guid id, CancellationToken ct = default);
}

public record CreateTemplateDto(
    string Name,
    string ContentTemplate,
    bool IsDefault);

public record UpdateTemplateDto(
    string? Name,
    string? ContentTemplate,
    bool? IsDefault);

public record TemplateDto(
    Guid Id,
    string Name,
    string ContentTemplate,
    bool IsDefault,
    bool IsSystem);
