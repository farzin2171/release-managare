using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Reconciliation;
using RepoManager.Application.Releases;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class ReleasesController : ControllerBase
{
    private readonly IReleaseService _service;
    private readonly IReleaseReconciliationService _reconciliation;

    public ReleasesController(IReleaseService service, IReleaseReconciliationService reconciliation)
    {
        _service = service;
        _reconciliation = reconciliation;
    }

    [HttpPost("projects/{id:guid}/releases")]
    public async Task<IActionResult> Create(Guid id, [FromBody] CreateReleaseDto dto, CancellationToken ct)
    {
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdStr, out var userId))
            return Unauthorized();

        var release = await _service.CreateAsync(id, dto, userId, ct);
        return StatusCode(201, release);
    }

    [HttpGet("releases/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var release = await _service.GetAsync(id, ct);
        return Ok(release);
    }

    [HttpPut("releases/{id:guid}")]
    public async Task<IActionResult> UpdateNotes(Guid id, [FromBody] UpdateNotesDto dto, CancellationToken ct)
    {
        var release = await _service.UpdateNotesAsync(id, dto, ct);
        return Ok(release);
    }

    [HttpPost("releases/{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        var release = await _service.PublishAsync(id, ct);
        return Ok(release);
    }

    [HttpPost("releases/{id:guid}/reconcile")]
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
}

public record AddJiraTicketsDto(IReadOnlyList<string> TicketKeys);
