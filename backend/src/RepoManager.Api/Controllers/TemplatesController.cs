using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.DTOs.Releases;
using RepoManager.Application.Services;
using RepoManager.Application.Templates;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
public class TemplatesController : ControllerBase
{
    private readonly IReleaseNoteTemplateService _service;
    private readonly IReleaseRenderService _renderService;

    public TemplatesController(IReleaseNoteTemplateService service, IReleaseRenderService renderService)
    {
        _service = service;
        _renderService = renderService;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var templates = await _service.ListAsync(ct);
        return Ok(templates);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTemplateDto dto, CancellationToken ct)
    {
        var template = await _service.CreateAsync(dto, ct);
        return StatusCode(201, template);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateTemplateDto dto, CancellationToken ct)
    {
        var template = await _service.UpdateAsync(id, dto, ct);
        return Ok(template);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("{id:guid}/clone")]
    public async Task<IActionResult> Clone(Guid id, CancellationToken ct)
    {
        var clone = await _service.CloneAsync(id, ct);
        return StatusCode(201, clone);
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(
        Guid id,
        [FromQuery] string contextSource = "synthetic",
        [FromQuery] Guid? projectId = null,
        CancellationToken ct = default)
    {
        if (string.Equals(contextSource, "project", StringComparison.OrdinalIgnoreCase) && projectId is null)
            return BadRequest(new { error = "projectId is required when contextSource=project" });

        var request = new TemplatePreviewRequest(contextSource, projectId);
        var result = await _renderService.PreviewTemplateAsync(id, request, ct);
        return Ok(result);
    }
}
