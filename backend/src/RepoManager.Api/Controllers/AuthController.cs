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
            AppendRefreshCookie(tokens.RefreshToken);
            return Ok(new { accessToken = tokens.AccessToken });
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
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var rawToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(rawToken))
            return Unauthorized(new { code = "refresh_token_missing" });

        var tokens = await _auth.RefreshAsync(new RefreshTokenDto(rawToken), ct);
        AppendRefreshCookie(tokens.RefreshToken);
        return Ok(new { accessToken = tokens.AccessToken });
    }

    private void AppendRefreshCookie(string refreshToken)
    {
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/api/v1/auth",
            MaxAge = TimeSpan.FromDays(30),
        });
    }
}
