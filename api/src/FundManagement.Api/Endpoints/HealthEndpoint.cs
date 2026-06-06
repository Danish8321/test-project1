using Dapper;
using FundManagement.Application.Common;

namespace FundManagement.Api.Endpoints;

public static class HealthEndpoint
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (IDbConnectionFactory dbFactory, ICircleClient circle) =>
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

            return Results.Ok(new
            {
                db = dbStatus,
                circle = circleStatus,
                timestamp = DateTime.UtcNow
            });
        })
        .WithName("Health");

        return app;
    }
}
