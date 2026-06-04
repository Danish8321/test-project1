using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Deposits;
using FundManagement.Application.Ledger;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class DepositService : IDepositService
{
    private readonly IDbConnectionFactory _db;
    private readonly ICircleClient _circle;
    private readonly ILedgerService _ledger;

    public DepositService(IDbConnectionFactory db, ICircleClient circle, ILedgerService ledger)
    {
        _db = db;
        _circle = circle;
        _ledger = ledger;
    }

    public async Task<IEnumerable<Deposit>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Deposit>(
            "SELECT * FROM deposits ORDER BY created_at DESC");
    }

    public async Task<IEnumerable<Deposit>> GetByCustomerAsync(Guid customerId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Deposit>(
            "SELECT * FROM deposits WHERE customer_id = @CustomerId ORDER BY created_at DESC",
            new { CustomerId = customerId });
    }

    public async Task<Deposit?> GetByIdAsync(Guid id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Deposit>(
            "SELECT * FROM deposits WHERE id = @Id", new { Id = id });
    }

    public async Task<Deposit> CreateAsync(Guid customerId, Guid fundingAccountId, decimal amount)
    {
        var intent = await _circle.CreatePaymentIntentAsync(
            amount, "USD", Guid.NewGuid().ToString());

        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<Deposit>(
            @"INSERT INTO deposits
                (id, customer_id, funding_account_id, circle_payment_intent_id, amount, status, created_at, updated_at)
              VALUES
                (uuid_generate_v4(), @CustomerId, @FundingAccountId, @CirclePaymentIntentId, @Amount, @Status, NOW(), NOW())
              RETURNING *",
            new
            {
                CustomerId = customerId,
                FundingAccountId = fundingAccountId,
                CirclePaymentIntentId = intent.Id,
                Amount = amount,
                Status = DepositStatus.Pending.ToString()
            });
    }

    public async Task ProcessSettlementAsync(string circlePaymentIntentId, string circleStatus)
    {
        using var conn = _db.CreateConnection();
        var deposit = await conn.QuerySingleOrDefaultAsync<Deposit>(
            "SELECT * FROM deposits WHERE circle_payment_intent_id = @Id",
            new { Id = circlePaymentIntentId });

        if (deposit == null) return;

        var newStatus = circleStatus == "complete" ? DepositStatus.Completed : DepositStatus.Failed;

        await conn.ExecuteAsync(
            "UPDATE deposits SET status = @Status, updated_at = NOW() WHERE id = @Id",
            new { Status = newStatus.ToString(), Id = deposit.Id });

        if (newStatus == DepositStatus.Completed)
            await _ledger.CreateEntryAsync(
                deposit.FundingAccountId, EntryType.Credit, deposit.Amount, circlePaymentIntentId);
    }
}
