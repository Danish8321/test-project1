using FundManagement.Api.Models.Requests;
using FundManagement.Application.Withdrawals;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("withdrawals")]
public class WithdrawalsController(IWithdrawalService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await svc.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var w = await svc.GetByIdAsync(id);
        return w is null ? NotFound() : Ok(w);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateWithdrawalRequest req)
    {
        try
        {
            var w = await svc.CreateAsync(
                req.CustomerId, req.FundingAccountId, req.Amount, req.DestinationAddress);
            return Created($"/withdrawals/{w.Id}", w);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
