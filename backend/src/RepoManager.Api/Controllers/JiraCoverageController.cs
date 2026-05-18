using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Jira;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1")]
[Authorize]
public class JiraCoverageController : ControllerBase
{
    private readonly IRepoJiraComparisonService _service;

    public JiraCoverageController(IRepoJiraComparisonService service)
    {
        _service = service;
    }

    [HttpGet("repositories/{id:guid}/jira-coverage")]
    public async Task<IActionResult> GetForRepo(
        Guid id,
        [FromQuery] bool refresh = false,
        CancellationToken ct = default)
    {
        try
        {
            var dto = await _service.GetForRepoAsync(id, refresh, ct);
            return Ok(dto);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("projects/{id:guid}/jira-coverage")]
    public async Task<IActionResult> GetForProject(
        Guid id,
        [FromQuery] bool refresh = false,
        CancellationToken ct = default)
    {
        try
        {
            var dto = await _service.GetForProjectAsync(id, refresh, ct);
            return Ok(dto);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("repositories/{id:guid}/jira-coverage/add-ticket")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AddTicket(
        Guid id,
        [FromBody] AddTicketRequest body,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(body.TicketKey))
            return BadRequest(new { detail = "ticketKey is required." });

        try
        {
            var result = await _service.AddTicketToFixVersionAsync(id, body.TicketKey, ct);
            return Ok(result);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (ConflictException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(new { detail = ex.Message });
        }
    }
}

public record AddTicketRequest(string TicketKey);
