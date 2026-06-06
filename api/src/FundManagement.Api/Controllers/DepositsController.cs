using FundManagement.Api.Models.Requests;
using FundManagement.Application.Deposits;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("deposits")]
public class DepositsController(IDepositService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await svc.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var d = await svc.GetByIdAsync(id);
        return d is null ? NotFound() : Ok(d);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateDepositRequest req)
    {
        try
        {
            var d = await svc.CreateAsync(req.CustomerId, req.FundingAccountId, req.Amount);
            return Created($"/deposits/{d.Id}", d);
        }
        catch (HttpRequestException ex)
        {
            return BadRequest(new { error = $"Circle API error: {ex.Message}" });
        }
    }
}
