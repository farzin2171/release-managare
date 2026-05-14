using RepoManager.Domain.Enums;

namespace RepoManager.Application.GitProviders;

public interface IGitProviderConnectionService
{
    Task<GitProviderConnectionDto> CreateAsync(CreateGitConnectionDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<GitProviderConnectionDto>> ListAsync(CancellationToken ct = default);
    Task<GitProviderConnectionDto> UpdateAsync(Guid id, UpdateGitConnectionDto dto, CancellationToken ct = default);
    Task<TestConnectionResultDto> TestAsync(TestGitConnectionDto dto, CancellationToken ct = default);
    Task<TestConnectionResultDto> TestByIdAsync(Guid id, CancellationToken ct = default);
    Task SyncAsync(Guid id, CancellationToken ct = default);
}

public record CreateGitConnectionDto(
    string Name,
    ProviderType ProviderType,
    string OrganizationUrl,
    string Pat);

public record UpdateGitConnectionDto(
    string? Name,
    string? OrganizationUrl,
    string? Pat);

public record TestGitConnectionDto(
    string OrganizationUrl,
    string Pat,
    ProviderType ProviderType);

public record GitProviderConnectionDto(
    Guid Id,
    string Name,
    ProviderType ProviderType,
    string OrganizationUrl,
    bool IsActive,
    DateTimeOffset? LastSyncedAt,
    string? LastTestStatus);

public record TestConnectionResultDto(bool Success, string Message);
