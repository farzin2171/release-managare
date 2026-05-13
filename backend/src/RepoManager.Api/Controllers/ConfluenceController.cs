using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Confluence;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/integrations/confluence")]
public class ConfluenceController : ControllerBase
{
    private readonly IConfluenceConnectionService _service;

    public ConfluenceController(IConfluenceConnectionService service) => _service = service;

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] UpsertConfluenceConnectionDto dto, CancellationToken ct)
    {
        var result = await _service.TestAsync(dto, ct);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var connection = await _service.GetAsync(ct);
        return connection is null ? NotFound() : Ok(connection);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut]
    public async Task<IActionResult> Upsert([FromBody] UpsertConfluenceConnectionDto dto, CancellationToken ct)
    {
        var connection = await _service.UpsertAsync(dto, ct);
        return Ok(connection);
    }
}
