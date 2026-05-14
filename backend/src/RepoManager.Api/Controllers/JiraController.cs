using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Jira;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/integrations/jira")]
public class JiraController : ControllerBase
{
    private readonly IJiraConnectionService _service;

    public JiraController(IJiraConnectionService service) => _service = service;

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] UpsertJiraConnectionDto dto, CancellationToken ct)
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
    public async Task<IActionResult> Upsert([FromBody] UpsertJiraConnectionDto dto, CancellationToken ct)
    {
        var connection = await _service.UpsertAsync(dto, ct);
        return Ok(connection);
    }

    [HttpGet("projects")]
    public async Task<IActionResult> ListProjects(CancellationToken ct)
    {
        var projects = await _service.ListProjectsAsync(ct);
        return Ok(projects);
    }
}
