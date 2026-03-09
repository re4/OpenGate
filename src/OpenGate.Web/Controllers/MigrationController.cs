using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenGate.Application.Migration;

namespace OpenGate.Web.Controllers;

[Route("api/migration")]
[ApiController]
[Authorize(Roles = "Admin")]
public class MigrationController(IMigrationService migrationService) : ControllerBase
{
    [HttpPost("test-connection")]
    public async Task<IActionResult> TestConnection([FromBody] ConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionString))
            return BadRequest("Connection string is required.");

        var result = await migrationService.TestConnectionAsync(request.ConnectionString);
        return Ok(result);
    }

    [HttpPost("start")]
    public async Task<IActionResult> StartMigration([FromBody] ConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConnectionString))
            return BadRequest("Connection string is required.");

        var progress = migrationService.GetProgress();
        if (progress.IsRunning)
            return Conflict("Migration is already running.");

        _ = Task.Run(() => migrationService.RunMigrationAsync(request.ConnectionString));

        return Ok(new { message = "Migration started." });
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(migrationService.GetProgress());
    }
}

public class ConnectionRequest
{
    public string ConnectionString { get; set; } = string.Empty;
}
