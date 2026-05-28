using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Services;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/custom-variables")]
[Authorize]
public class ProjectCustomVariablesController : ControllerBase
{
    private readonly IProjectCustomVariableService _service;

    public ProjectCustomVariablesController(IProjectCustomVariableService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId, CancellationToken ct)
    {
        var vars = await _service.GetAllAsync(projectId, ct);
        return Ok(vars);
    }

    [HttpPut("{key}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Upsert(
        Guid projectId,
        string key,
        [FromBody] UpsertCustomVariableRequest request,
        CancellationToken ct)
    {
        var variable = await _service.UpsertAsync(projectId, key, request.Value, ct);
        return Ok(variable);
    }

    [HttpDelete("{key}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(
        Guid projectId,
        string key,
        CancellationToken ct)
    {
        await _service.DeleteAsync(projectId, key, ct);
        return NoContent();
    }
}

public record UpsertCustomVariableRequest(string Value);
