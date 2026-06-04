using FundManagement.Application.Deposits;

namespace FundManagement.Api.Endpoints;

public static class DepositEndpoints
{
    public static void MapDepositEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/deposits").WithTags("Deposits");

        g.MapGet("/", async (IDepositService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        g.MapGet("/{id:guid}", async (Guid id, IDepositService svc) =>
        {
            var d = await svc.GetByIdAsync(id);
            return d is null ? Results.NotFound() : Results.Ok(d);
        });

        g.MapPost("/", async (CreateDepositRequest req, IDepositService svc) =>
        {
            try
            {
                var d = await svc.CreateAsync(req.CustomerId, req.FundingAccountId, req.Amount);
                return Results.Created($"/deposits/{d.Id}", d);
            }
            catch (HttpRequestException ex)
            {
                return Results.BadRequest(new { error = $"Circle API error: {ex.Message}" });
            }
        });
    }
}

public record CreateDepositRequest(Guid CustomerId, Guid FundingAccountId, decimal Amount);
