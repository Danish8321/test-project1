using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Ledger;
using FundManagement.Application.Withdrawals;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class WithdrawalService : IWithdrawalService
{
    private readonly IDbConnectionFactory _db;
    private readonly ICircleClient _circle;
    private readonly ILedgerService _ledger;

    public WithdrawalService(IDbConnectionFactory db, ICircleClient circle, ILedgerService ledger)
    {
        _db = db;
        _circle = circle;
        _ledger = ledger;
    }

    public async Task<IEnumerable<Withdrawal>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Withdrawal>(
            "SELECT * FROM withdrawals ORDER BY created_at DESC");
    }

    public async Task<IEnumerable<Withdrawal>> GetByCustomerAsync(Guid customerId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<Withdrawal>(
            "SELECT * FROM withdrawals WHERE customer_id = @CustomerId ORDER BY created_at DESC",
            new { CustomerId = customerId });
    }

    public async Task<Withdrawal?> GetByIdAsync(Guid id)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleOrDefaultAsync<Withdrawal>(
            "SELECT * FROM withdrawals WHERE id = @Id", new { Id = id });
    }

    public async Task<Withdrawal> CreateAsync(
        Guid customerId, Guid fundingAccountId, decimal amount, string destinationAddress)
    {
        var balance = await _ledger.GetBalanceAsync(fundingAccountId);
        if (balance < amount)
            throw new InvalidOperationException($"Insufficient balance: {balance} < {amount}");

        // Sep 2025 relaunch: must register address in address book first, then payout via recipient ID.
        var recipient = await _circle.CreateRecipientAsync(destinationAddress, "ETH", Guid.NewGuid().ToString());

        // Wait for recipient to become active (sandbox: seconds; production: depends on delayed-withdrawal setting)
        for (var i = 0; i < 6 && recipient.Status != "active"; i++)
        {
            await Task.Delay(5_000);
            recipient = await _circle.GetRecipientAsync(recipient.Id);
        }
        if (recipient.Status != "active")
            throw new InvalidOperationException($"Circle recipient did not activate within timeout: {recipient.Id}");

        var payout = await _circle.CreatePayoutAsync(amount, "USD", recipient.Id, Guid.NewGuid().ToString());

        using var conn = _db.CreateConnection();
        var withdrawal = await conn.QuerySingleAsync<Withdrawal>(
            @"INSERT INTO withdrawals
                (id, customer_id, funding_account_id, circle_payout_id, amount, status, created_at, updated_at)
              VALUES
                (uuid_generate_v4(), @CustomerId, @FundingAccountId, @CirclePayoutId, @Amount, @Status, NOW(), NOW())
              RETURNING *",
            new
            {
                CustomerId = customerId,
                FundingAccountId = fundingAccountId,
                CirclePayoutId = payout.Id,
                Amount = amount,
                Status = WithdrawalStatus.Pending.ToString()
            });

        await _ledger.CreateEntryAsync(
            fundingAccountId, EntryType.Debit, amount, payout.Id);

        return withdrawal;
    }

    public async Task ProcessPayoutSettlementAsync(string circlePayoutId, string circleStatus)
    {
        using var conn = _db.CreateConnection();
        var newStatus = circleStatus == "complete"
            ? WithdrawalStatus.Completed
            : WithdrawalStatus.Failed;

        await conn.ExecuteAsync(
            "UPDATE withdrawals SET status = @Status, updated_at = NOW() WHERE circle_payout_id = @PayoutId",
            new { Status = newStatus.ToString(), PayoutId = circlePayoutId });

        if (newStatus == WithdrawalStatus.Failed)
        {
            var withdrawal = await conn.QuerySingleOrDefaultAsync<Withdrawal>(
                "SELECT * FROM withdrawals WHERE circle_payout_id = @PayoutId",
                new { PayoutId = circlePayoutId });

            if (withdrawal != null)
                await _ledger.CreateEntryAsync(
                    withdrawal.FundingAccountId, EntryType.Credit,
                    withdrawal.Amount, $"reversal:{circlePayoutId}");
        }
    }
}
