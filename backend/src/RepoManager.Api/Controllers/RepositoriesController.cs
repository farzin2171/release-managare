using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Repositories;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/repositories")]
public class RepositoriesController : ControllerBase
{
    private readonly IRepositoryService _service;

    public RepositoriesController(IRepositoryService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] Guid? connectionId,
        [FromQuery] bool? isTracked,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var query = new ListRepositoriesQuery(connectionId, isTracked, search);
        var repos = await _service.ListAsync(query, ct);
        return Ok(repos);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> SetTracked(Guid id, [FromBody] SetTrackedDto dto, CancellationToken ct)
    {
        var repo = await _service.SetTrackedAsync(id, dto, ct);
        return Ok(repo);
    }
}
