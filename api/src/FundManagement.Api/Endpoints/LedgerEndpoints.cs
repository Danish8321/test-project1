using FundManagement.Application.Ledger;

namespace FundManagement.Api.Endpoints;

public static class LedgerEndpoints
{
    public static void MapLedgerEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/funding-accounts").WithTags("Ledger");

        g.MapGet("/{id:guid}/ledger", async (Guid id, ILedgerService svc) =>
            Results.Ok(await svc.GetByFundingAccountAsync(id)));

        g.MapGet("/{id:guid}/balance", async (Guid id, ILedgerService svc) =>
            Results.Ok(new { balance = await svc.GetBalanceAsync(id) }));
    }
}
