using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Api.Filters;
using RepoManager.Application.Auth;
using RepoManager.Application.Common.Exceptions;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [AllowAnonymous]
    [HttpPost("setup")]
    [ServiceFilter(typeof(SetupKeyAuthorizationFilter))]
    public async Task<IActionResult> Setup([FromBody] SetupDto dto, CancellationToken ct)
    {
        if (await _auth.AdminExistsAsync(ct))
            return StatusCode(409, new { code = "setup_already_complete" });

        var user = await _auth.SetupAsync(dto, ct);
        return StatusCode(201, user);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto, CancellationToken ct)
    {
        try
        {
            var tokens = await _auth.LoginAsync(dto, ct);
            return Ok(tokens);
        }
        catch (NotFoundException)
        {
            return Unauthorized();
        }
        catch (ConflictException)
        {
            return Unauthorized();
        }
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto, CancellationToken ct)
    {
        var tokens = await _auth.RefreshAsync(dto, ct);
        return Ok(tokens);
    }
}
