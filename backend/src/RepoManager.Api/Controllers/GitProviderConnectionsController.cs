using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.GitProviders;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/integrations/git")]
public class GitProviderConnectionsController : ControllerBase
{
    private readonly IGitProviderConnectionService _service;

    public GitProviderConnectionsController(IGitProviderConnectionService service) => _service = service;

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] TestGitConnectionDto dto, CancellationToken ct)
    {
        var result = await _service.TestAsync(dto, ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var connections = await _service.ListAsync(ct);
        return Ok(connections);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGitConnectionDto dto, CancellationToken ct)
    {
        var connection = await _service.CreateAsync(dto, ct);
        return StatusCode(201, connection);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGitConnectionDto dto, CancellationToken ct)
    {
        var connection = await _service.UpdateAsync(id, dto, ct);
        return Ok(connection);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/sync")]
    public async Task<IActionResult> Sync(Guid id, CancellationToken ct)
    {
        await _service.SyncAsync(id, ct);
        return Accepted(new { message = "Sync started", connectionId = id });
    }
}
