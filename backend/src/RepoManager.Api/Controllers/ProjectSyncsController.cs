using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Events;
using RepoManager.Application.Services;

namespace RepoManager.Api.Controllers;

[ApiController]
[Authorize]
public class ProjectSyncsController : ControllerBase
{
    private readonly IProjectSyncService _syncService;
    private readonly IProjectSyncEventPublisher _projectEvents;

    public ProjectSyncsController(IProjectSyncService syncService, IProjectSyncEventPublisher projectEvents)
    {
        _syncService = syncService;
        _projectEvents = projectEvents;
    }

    [HttpPost("api/v1/projects/{id:guid}/sync")]
    public async Task<IActionResult> Enqueue(Guid id, CancellationToken ct)
    {
        var dto = await _syncService.EnqueueAsync(id, GetUserId(), ct);
        return StatusCode(202, dto);
    }

    [HttpDelete("api/v1/projects/{id:guid}/sync/active")]
    public async Task<IActionResult> CancelActive(Guid id, CancellationToken ct)
    {
        await _syncService.CancelActiveAsync(id, ct);
        return Ok(new { message = "Cancellation requested. The in-progress repository will complete before the run stops." });
    }

    [HttpGet("api/v1/projects/{id:guid}/sync/latest")]
    public async Task<IActionResult> GetLatest(Guid id, CancellationToken ct)
    {
        var dto = await _syncService.GetLatestAsync(id, ct);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    [HttpGet("api/v1/projects/{id:guid}/sync/active")]
    public async Task<IActionResult> GetActive(Guid id, CancellationToken ct)
    {
        var dto = await _syncService.GetActiveAsync(id, ct);
        if (dto is null) return StatusCode(204);
        return Ok(dto);
    }

    [HttpGet("api/v1/projects/{id:guid}/sync/active/stream")]
    public async Task StreamSyncEvents(Guid id, [FromHeader(Name = "Last-Event-ID")] string? lastEventIdStr, CancellationToken ct)
    {
        var active = await _syncService.GetActiveAsync(id, ct);
        if (active is null)
        {
            Response.StatusCode = 204;
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        long? lastEventId = long.TryParse(lastEventIdStr, out var parsed) ? parsed : null;

        await foreach (var msg in _projectEvents.SubscribeAsync(active.Id, lastEventId, ct))
        {
            await Response.WriteAsync($"id: {msg.Id}\nevent: {msg.Event}\ndata: {msg.Data}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID claim missing.");
        return Guid.Parse(claim);
    }
}
