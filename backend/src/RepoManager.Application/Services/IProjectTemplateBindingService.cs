using RepoManager.Application.DTOs.Bindings;

namespace RepoManager.Application.Services;

public interface IProjectTemplateBindingService
{
    Task<IReadOnlyList<ProjectTemplateBindingDto>> GetAllAsync(
        Guid projectId, CancellationToken ct = default);

    Task<ProjectTemplateBindingDto> CreateAsync(
        Guid projectId, CreateBindingRequest request, CancellationToken ct = default);

    Task<ProjectTemplateBindingDto> UpdateAsync(
        Guid projectId, Guid bindingId, UpdateBindingRequest request, CancellationToken ct = default);

    Task DeleteAsync(
        Guid projectId, Guid bindingId, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectTemplateBindingDto>> ReorderAsync(
        Guid projectId, IReadOnlyList<Guid> orderedIds, CancellationToken ct = default);
}
