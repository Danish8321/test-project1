using FundManagement.Api.Models.Requests;
using FundManagement.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("customers")]
public class CustomersController(ICustomerService svc) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await svc.GetAllAsync());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var c = await svc.GetByIdAsync(id);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateCustomerRequest req)
    {
        var c = await svc.CreateAsync(req.Name, req.Email, req.CustomerType);
        return Created($"/customers/{c.Id}", c);
    }

    [HttpGet("{id:guid}/funding-accounts")]
    public async Task<IActionResult> GetFundingAccounts(Guid id) =>
        Ok(await svc.GetFundingAccountsAsync(id));

    [HttpPost("{id:guid}/funding-accounts")]
    public async Task<IActionResult> CreateFundingAccount(Guid id, CreateFundingAccountRequest req)
    {
        var fa = await svc.CreateFundingAccountAsync(id, req.Currency);
        return Created($"/customers/{id}/funding-accounts/{fa.Id}", fa);
    }
}
