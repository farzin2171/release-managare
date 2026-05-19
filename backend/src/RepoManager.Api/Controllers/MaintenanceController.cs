using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RepoManager.Application.Maintenance;

namespace RepoManager.Api.Controllers;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
public class MaintenanceController : ControllerBase
{
    private readonly IDatabaseMaintenanceService _maintenance;

    public MaintenanceController(IDatabaseMaintenanceService maintenance) => _maintenance = maintenance;

    [HttpPost("database/reset")]
    public async Task<IActionResult> ResetDatabase(CancellationToken ct)
    {
        await _maintenance.ResetDatabaseAsync(ct);
        return NoContent();
    }
}
