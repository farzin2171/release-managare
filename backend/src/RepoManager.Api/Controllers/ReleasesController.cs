using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Releases;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class ReleasesController : ControllerBase
{
    private readonly IReleaseService _service;

    public ReleasesController(IReleaseService service) => _service = service;

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
}
