using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Templates;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/templates")]
public class TemplatesController : ControllerBase
{
    private readonly IReleaseNoteTemplateService _service;

    public TemplatesController(IReleaseNoteTemplateService service) => _service = service;

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
}
