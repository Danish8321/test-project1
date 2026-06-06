using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Customers;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class CustomerService : ICustomerService
{
    private readonly IDbConnectionFactory _db;

    public CustomerService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<Customer>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Customer>(
            "SELECT * FROM customers ORDER BY created_at DESC");
    }

    public async Task<Customer?> GetByIdAsync(Guid id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Customer>(
            "SELECT * FROM customers WHERE id = @Id", new { Id = id });
    }

    public async Task<Customer> CreateAsync(string name, string email, CustomerType type)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<Customer>(
            @"INSERT INTO customers (id, name, email, customer_type, created_at)
              VALUES (uuid_generate_v4(), @Name, @Email, @Type, NOW())
              RETURNING *",
            new { Name = name, Email = email, Type = type.ToString() });
    }

    public async Task<IEnumerable<FundingAccount>> GetFundingAccountsAsync(Guid customerId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<FundingAccount>(
            "SELECT * FROM funding_accounts WHERE customer_id = @CustomerId ORDER BY created_at DESC",
            new { CustomerId = customerId });
    }

    public async Task<FundingAccount> CreateFundingAccountAsync(Guid customerId, string currency)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<FundingAccount>(
            @"INSERT INTO funding_accounts (id, customer_id, currency, created_at)
              VALUES (uuid_generate_v4(), @CustomerId, @Currency, NOW())
              RETURNING *",
            new { CustomerId = customerId, Currency = currency });
    }
}
