using System.Security.Claims;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Application.Repositories;
using AppValidationException = RepoManager.Application.Common.Exceptions.ValidationException;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/repositories")]
public class RepositoriesController : ControllerBase
{
    private readonly IRepositoryService _service;
    private readonly IValidator<SetLatestTagDto> _setTagValidator;
    private readonly IValidator<UpdateRepositoryRequest> _updateValidator;

    public RepositoriesController(
        IRepositoryService service,
        IValidator<SetLatestTagDto> setTagValidator,
        IValidator<UpdateRepositoryRequest> updateValidator)
    {
        _service = service;
        _setTagValidator = setTagValidator;
        _updateValidator = updateValidator;
    }

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

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRepositoryRequest dto, CancellationToken ct)
    {
        var validation = await _updateValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return UnprocessableEntity(validation.ToDictionary());

        try
        {
            var repo = await _service.UpdateAsync(id, dto, ct);
            return Ok(repo);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
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
        var query = new GetChangesQuery(groupBy, type, contributor, search);
        var changes = await _service.GetChangesAsync(id, query, ct);
        return Ok(changes);
    }

    [Authorize]
    [HttpGet("{id:guid}/tags")]
    public async Task<IActionResult> GetTags(Guid id, CancellationToken ct)
    {
        try
        {
            var tags = await _service.GetTagsAsync(id, ct);
            return Ok(new { tags });
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (AppValidationException)
        {
            return UnprocessableEntity();
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("{id:guid}/latest-tag")]
    public async Task<IActionResult> SetLatestTag(Guid id, [FromBody] SetLatestTagDto dto, CancellationToken ct)
    {
        var validation = await _setTagValidator.ValidateAsync(dto, ct);
        if (!validation.IsValid)
            return UnprocessableEntity(validation.ToDictionary());

        var actingUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            var repo = await _service.SetLatestTagAsync(id, dto.TagName, actingUserId, ct);
            return Ok(repo);
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
        catch (AppValidationException)
        {
            return UnprocessableEntity();
        }
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id:guid}/latest-tag")]
    public async Task<IActionResult> ClearLatestTag(Guid id, CancellationToken ct)
    {
        var actingUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        try
        {
            await _service.ClearLatestTagAsync(id, actingUserId, ct);
            return NoContent();
        }
        catch (NotFoundException)
        {
            return NotFound();
        }
    }
}
