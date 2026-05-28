using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.DTOs.Bindings;
using RepoManager.Application.Services;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/projects/{projectId:guid}/template-bindings")]
[Authorize]
public class ProjectTemplateBindingsController : ControllerBase
{
    private readonly IProjectTemplateBindingService _service;

    public ProjectTemplateBindingsController(IProjectTemplateBindingService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId, CancellationToken ct)
    {
        var bindings = await _service.GetAllAsync(projectId, ct);
        return Ok(bindings);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(
        Guid projectId,
        [FromBody] CreateBindingRequest request,
        CancellationToken ct)
    {
        var binding = await _service.CreateAsync(projectId, request, ct);
        return StatusCode(201, binding);
    }

    [HttpPut("{bindingId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(
        Guid projectId,
        Guid bindingId,
        [FromBody] UpdateBindingRequest request,
        CancellationToken ct)
    {
        var binding = await _service.UpdateAsync(projectId, bindingId, request, ct);
        return Ok(binding);
    }

    [HttpDelete("{bindingId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(
        Guid projectId,
        Guid bindingId,
        CancellationToken ct)
    {
        await _service.DeleteAsync(projectId, bindingId, ct);
        return NoContent();
    }

    [HttpPost("reorder")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Reorder(
        Guid projectId,
        [FromBody] ReorderBindingsRequest request,
        CancellationToken ct)
    {
        var bindings = await _service.ReorderAsync(projectId, request.OrderedIds, ct);
        return Ok(bindings);
    }
}

public record ReorderBindingsRequest(IReadOnlyList<Guid> OrderedIds);
