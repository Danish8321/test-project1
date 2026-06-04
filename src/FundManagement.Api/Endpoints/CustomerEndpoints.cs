using FundManagement.Application.Customers;
using FundManagement.Domain.Enums;

namespace FundManagement.Api.Endpoints;

public static class CustomerEndpoints
{
    public static void MapCustomerEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/customers").WithTags("Customers");

        g.MapGet("/", async (ICustomerService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        g.MapGet("/{id:guid}", async (Guid id, ICustomerService svc) =>
        {
            var c = await svc.GetByIdAsync(id);
            return c is null ? Results.NotFound() : Results.Ok(c);
        });

        g.MapPost("/", async (CreateCustomerRequest req, ICustomerService svc) =>
        {
            var c = await svc.CreateAsync(req.Name, req.Email, req.CustomerType);
            return Results.Created($"/customers/{c.Id}", c);
        });

        g.MapGet("/{id:guid}/funding-accounts", async (Guid id, ICustomerService svc) =>
            Results.Ok(await svc.GetFundingAccountsAsync(id)));

        g.MapPost("/{id:guid}/funding-accounts", async (Guid id, CreateFundingAccountRequest req, ICustomerService svc) =>
        {
            var fa = await svc.CreateFundingAccountAsync(id, req.Currency);
            return Results.Created($"/customers/{id}/funding-accounts/{fa.Id}", fa);
        });
    }
}

public record CreateCustomerRequest(string Name, string Email, CustomerType CustomerType);
public record CreateFundingAccountRequest(string Currency);
