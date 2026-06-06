using Dapper;
using FundManagement.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController(IDbConnectionFactory dbFactory, ICircleClient circle) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetHealth()
    {
        var dbStatus = "error";
        var circleStatus = "error";

        try
        {
            using var db = dbFactory.CreateConnection();
            await db.ExecuteScalarAsync("SELECT 1");
            dbStatus = "ok";
        }
        catch { }

        try
        {
            circleStatus = await circle.PingAsync() ? "ok" : "error";
        }
        catch { }

        return Ok(new
        {
            db = dbStatus,
            circle = circleStatus,
            timestamp = DateTime.UtcNow
        });
    }
}
