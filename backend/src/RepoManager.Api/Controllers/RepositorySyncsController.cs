using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Services;

namespace RepoManager.Api.Controllers;

[ApiController]
[Authorize]
public class RepositorySyncsController : ControllerBase
{
    private readonly IRepositorySyncService _syncService;

    public RepositorySyncsController(IRepositorySyncService syncService) => _syncService = syncService;

    [HttpPost("api/v1/repositories/{id:guid}/sync")]
    public async Task<IActionResult> Enqueue(Guid id, CancellationToken ct)
    {
        var userId = GetUserId();
        var dto = await _syncService.EnqueueAsync(id, userId, ct);
        return StatusCode(202, dto);
    }

    [HttpGet("api/v1/repositories/{id:guid}/sync/latest")]
    public async Task<IActionResult> GetLatest(Guid id, CancellationToken ct)
    {
        var dto = await _syncService.GetLatestAsync(id, ct);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    [HttpGet("api/v1/repository-syncs/{syncId:guid}")]
    public async Task<IActionResult> GetById(Guid syncId, CancellationToken ct)
    {
        var dto = await _syncService.GetByIdAsync(syncId, ct);
        if (dto is null) return NotFound();
        return Ok(dto);
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID claim missing.");
        return Guid.Parse(claim);
    }
}
