using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Projects;
using RepoManager.Application.Repositories;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/projects")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _service;

    public ProjectsController(IProjectService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var projects = await _service.ListAsync(ct);
        return Ok(projects);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectDto dto, CancellationToken ct)
    {
        var project = await _service.CreateAsync(dto, ct);
        return StatusCode(201, project);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var project = await _service.GetAsync(id, ct);
        return Ok(project);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDto dto, CancellationToken ct)
    {
        var project = await _service.UpdateAsync(id, dto, ct);
        return Ok(project);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/repositories/{repoId:guid}")]
    public async Task<IActionResult> AssignRepository(Guid id, Guid repoId, [FromBody] AssignRepositoryDto dto, CancellationToken ct)
    {
        var project = await _service.AssignRepositoryAsync(id, repoId, dto, ct);
        return Ok(project);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id:guid}/repositories/{repoId:guid}")]
    public async Task<IActionResult> RemoveRepository(Guid id, Guid repoId, CancellationToken ct)
    {
        var project = await _service.RemoveRepositoryAsync(id, repoId, ct);
        return Ok(project);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("{id:guid}/jira")]
    public async Task<IActionResult> ConfigureJira(Guid id, [FromBody] ConfigureJiraDto dto, CancellationToken ct)
    {
        var project = await _service.ConfigureJiraAsync(id, dto, ct);
        return Ok(project);
    }

    [HttpGet("{id:guid}/changes")]
    public async Task<IActionResult> GetChanges(
        Guid id,
        [FromQuery] string groupBy = "ticket",
        [FromQuery] string? type = null,
        [FromQuery] string? contributor = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _service.GetChangesAsync(id, new GetChangesQuery(groupBy, type, contributor, search), ct);
        return Ok(result);
    }
}
