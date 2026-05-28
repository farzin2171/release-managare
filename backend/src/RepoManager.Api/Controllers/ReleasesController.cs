using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.DTOs.Releases;
using RepoManager.Application.Reconciliation;
using RepoManager.Application.Releases;
using RepoManager.Application.Services;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class ReleasesController : ControllerBase
{
    private readonly IReleaseService _service;
    private readonly IReleaseCompositionService _composition;
    private readonly IReleaseReconciliationService _reconciliation;
    private readonly IReleaseRenderService _render;

    public ReleasesController(
        IReleaseService service,
        IReleaseCompositionService composition,
        IReleaseReconciliationService reconciliation,
        IReleaseRenderService render)
    {
        _service = service;
        _composition = composition;
        _reconciliation = reconciliation;
        _render = render;
    }

    // ── Composition-based endpoints (US1 + US2) ──────────────────────────────

    [HttpPost("projects/{projectId:guid}/releases/preview")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PreviewRelease(
        Guid projectId,
        [FromBody] ReleasePreviewRequest request,
        CancellationToken ct)
    {
        var preview = await _composition.PreviewAsync(projectId, request.RepositoryIds, ct);
        return Ok(preview);
    }

    [HttpGet("projects/{projectId:guid}/releases")]
    public async Task<IActionResult> ListByProject(
        Guid projectId,
        [FromQuery] string? status,
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] string? order,
        CancellationToken ct)
    {
        var releases = await _composition.ListByProjectAsync(projectId, status, search, sort, order, ct);
        return Ok(releases);
    }

    [HttpPost("projects/{projectId:guid}/releases")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateDraft(
        Guid projectId,
        [FromBody] CreateReleaseRequest request,
        CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var release = await _composition.CreateDraftAsync(projectId, request, userId.Value, ct);
        return StatusCode(201, release);
    }

    [HttpGet("projects/{projectId:guid}/releases/{id:guid}")]
    public async Task<IActionResult> GetRelease(Guid projectId, Guid id, [FromQuery] string? mode, CancellationToken ct)
    {
        var release = await _composition.GetAsync(id, ct);

        if (string.Equals(mode, "edit", StringComparison.OrdinalIgnoreCase))
        {
            var userId = GetUserId();
            var userName = GetUserName();
            if (userId is null) return Unauthorized();

            var blocker = await _composition.TryAcquireEditLockAsync(id, userId.Value, userName, ct);
            if (blocker is not null)
                return Conflict(new { code = "edit_locked", lockedBy = blocker });
        }

        return Ok(release);
    }

    [HttpPut("projects/{projectId:guid}/releases/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateDraft(
        Guid projectId,
        Guid id,
        [FromBody] UpdateReleaseRequest request,
        CancellationToken ct)
    {
        var release = await _composition.UpdateDraftAsync(id, request, ct);
        return Ok(release);
    }

    [HttpDelete("projects/{projectId:guid}/releases/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDraft(Guid projectId, Guid id, CancellationToken ct)
    {
        await _composition.DeleteDraftAsync(id, ct);
        return NoContent();
    }

    [HttpDelete("projects/{projectId:guid}/releases/{id:guid}/lock")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ReleaseLock(Guid projectId, Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        await _composition.ReleaseEditLockAsync(id, userId.Value, ct);
        return NoContent();
    }

    // ── Legacy release endpoints (kept for existing publish/reconcile flows) ──

    [HttpGet("releases/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var release = await _service.GetAsync(id, ct);
        return Ok(release);
    }

    [HttpPut("releases/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateNotes(Guid id, [FromBody] UpdateNotesDto dto, CancellationToken ct)
    {
        var release = await _service.UpdateNotesAsync(id, dto, ct);
        return Ok(release);
    }

    [HttpPost("releases/{id:guid}/publish")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var release = await _service.PublishAsync(id, ct);
        return Ok(release);
    }

    [HttpPost("releases/{id:guid}/reconcile")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reconcile(Guid id, CancellationToken ct)
    {
        var result = await _reconciliation.ReconcileAsync(id, ct);
        return Ok(result);
    }

    [HttpGet("releases/{id:guid}/reconciliation")]
    public async Task<IActionResult> GetReconciliation(Guid id, CancellationToken ct)
    {
        var result = await _reconciliation.GetLatestAsync(id, ct);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("releases/{id:guid}/reconciliation/jira-tickets")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddJiraTickets(Guid id, [FromBody] AddJiraTicketsDto dto, CancellationToken ct)
    {
        await _reconciliation.AddGitTicketsToJiraAsync(id, dto.TicketKeys, ct);
        return NoContent();
    }

    // ── Render / wizard endpoints ────────────────────────────────────────────

    [HttpPost("releases/{id:guid}/prepare-pages")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PreparePages(
        Guid id,
        [FromBody] PreparePageRequest request,
        CancellationToken ct)
    {
        var result = await _render.PrepareAsync(id, request, ct);
        return Ok(result);
    }

    [HttpPost("releases/{id:guid}/publish-pages")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> PublishPages(
        Guid id,
        [FromBody] PublishPagesRequest request,
        CancellationToken ct)
    {
        var result = await _render.PublishAsync(id, request, ct);
        return Ok(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Guid? GetUserId()
    {
        var val = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(val, out var id) ? id : null;
    }

    private string GetUserName()
        => User.FindFirst(ClaimTypes.Name)?.Value
        ?? User.FindFirst(ClaimTypes.Email)?.Value
        ?? "unknown";
}

public record AddJiraTicketsDto(IReadOnlyList<string> TicketKeys);

public record ReleasePreviewRequest(IReadOnlyList<Guid> RepositoryIds);
