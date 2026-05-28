using RepoManager.Application.DTOs.CustomVariables;

namespace RepoManager.Application.Services;

public interface IProjectCustomVariableService
{
    Task<IReadOnlyList<ProjectCustomVariableDto>> GetAllAsync(
        Guid projectId, CancellationToken ct = default);

    Task<ProjectCustomVariableDto> UpsertAsync(
        Guid projectId, string key, string value, CancellationToken ct = default);

    Task DeleteAsync(
        Guid projectId, string key, CancellationToken ct = default);
}
