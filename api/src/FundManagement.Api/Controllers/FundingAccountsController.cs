using FundManagement.Application.Ledger;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("funding-accounts")]
public class FundingAccountsController(ILedgerService svc) : ControllerBase
{
    [HttpGet("{id:guid}/ledger")]
    public async Task<IActionResult> GetLedger(Guid id) =>
        Ok(await svc.GetByFundingAccountAsync(id));

    [HttpGet("{id:guid}/balance")]
    public async Task<IActionResult> GetBalance(Guid id) =>
        Ok(new { balance = await svc.GetBalanceAsync(id) });
}
