using FundManagement.Application.Reconciliation;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("reconciliation")]
public class ReconciliationController(IReconciliationService svc) : ControllerBase
{
    [HttpPost("run")]
    public async Task<IActionResult> Run() =>
        Ok(await svc.RunAsync());
}
