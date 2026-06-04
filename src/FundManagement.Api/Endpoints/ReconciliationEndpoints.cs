using FundManagement.Application.Reconciliation;

namespace FundManagement.Api.Endpoints;

public static class ReconciliationEndpoints
{
    public static void MapReconciliationEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/reconciliation").WithTags("Reconciliation");

        g.MapPost("/run", async (IReconciliationService svc) =>
            Results.Ok(await svc.RunAsync()));
    }
}
