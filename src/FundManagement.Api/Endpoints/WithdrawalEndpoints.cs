using FundManagement.Application.Withdrawals;

namespace FundManagement.Api.Endpoints;

public static class WithdrawalEndpoints
{
    public static void MapWithdrawalEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/withdrawals").WithTags("Withdrawals");

        g.MapGet("/", async (IWithdrawalService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        g.MapGet("/{id:guid}", async (Guid id, IWithdrawalService svc) =>
        {
            var w = await svc.GetByIdAsync(id);
            return w is null ? Results.NotFound() : Results.Ok(w);
        });

        g.MapPost("/", async (CreateWithdrawalRequest req, IWithdrawalService svc) =>
        {
            try
            {
                var w = await svc.CreateAsync(
                    req.CustomerId, req.FundingAccountId, req.Amount, req.DestinationAddress);
                return Results.Created($"/withdrawals/{w.Id}", w);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });
    }
}

public record CreateWithdrawalRequest(Guid CustomerId, Guid FundingAccountId, decimal Amount, string DestinationAddress);
