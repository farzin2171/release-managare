using RepoManager.Application.DTOs.Releases;

namespace RepoManager.Application.Services;

public interface IReleaseRenderService
{
    Task<PreparedReleaseDto> PrepareAsync(
        Guid releaseId,
        PreparePageRequest request,
        CancellationToken ct = default);

    Task<PublishResultDto> PublishAsync(
        Guid releaseId,
        PublishPagesRequest request,
        CancellationToken ct = default);

    Task<TemplatePreviewDto> PreviewTemplateAsync(
        Guid templateId,
        TemplatePreviewRequest request,
        CancellationToken ct = default);
}
