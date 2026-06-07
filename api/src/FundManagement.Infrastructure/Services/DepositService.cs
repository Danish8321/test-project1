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

    public async Task UpdateDepositAddressAsync(string paymentIntentId, string? address, string? chain, DateTimeOffset? expiresOn)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE deposits
              SET deposit_address = @Address, chain = @Chain, expires_on = @ExpiresOn,
                  status = @Status, updated_at = NOW()
              WHERE circle_payment_intent_id = @Id AND status = @PendingStatus",
            new
            {
                Address = address,
                Chain = chain,
                ExpiresOn = expiresOn,
                Status = DepositStatus.PendingCustomerTransfer.ToString(),
                Id = paymentIntentId,
                PendingStatus = DepositStatus.Pending.ToString()
            });
    }

    public async Task MarkPaymentDetectedAsync(string paymentIntentId, string paymentId, string? transactionHash)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE deposits
              SET circle_payment_id = @PaymentId, transaction_hash = @TxHash,
                  status = @Status, updated_at = NOW()
              WHERE circle_payment_intent_id = @Id
                AND status NOT IN (@Completed, @Failed)",
            new
            {
                PaymentId = paymentId,
                TxHash = transactionHash,
                Status = DepositStatus.PaymentDetected.ToString(),
                Id = paymentIntentId,
                Completed = DepositStatus.Completed.ToString(),
                Failed = DepositStatus.Failed.ToString()
            });
    }

    public async Task ProcessSettlementByPaymentAsync(string paymentId, string paymentIntentId, decimal settlementAmount)
    {
        using var conn = _db.CreateConnection();

        // Transaction + SELECT FOR UPDATE:
        // - FOR UPDATE locks the deposit row, serializing concurrent SNS deliveries of the same payment
        // - Transaction ensures deposit status update and ledger credit are atomic:
        //   if the ledger INSERT fails, the deposit rolls back to non-Completed so the next retry can try again
        using var tx = conn.BeginTransaction();
        try
        {
            var deposit = await conn.QuerySingleOrDefaultAsync<Deposit>(
                "SELECT * FROM deposits WHERE circle_payment_intent_id = @Id FOR UPDATE",
                new { Id = paymentIntentId }, transaction: tx);

            if (deposit == null || deposit.Status == DepositStatus.Completed)
            {
                tx.Rollback();
                return;
            }

            await conn.ExecuteAsync(
                @"UPDATE deposits
                  SET status = @Status, circle_payment_id = @PaymentId, updated_at = NOW()
                  WHERE id = @Id",
                new { Status = DepositStatus.Completed.ToString(), PaymentId = paymentId, Id = deposit.Id },
                transaction: tx);

            // Ledger INSERT on the same connection inside the same transaction.
            // ON CONFLICT (reference_id) DO NOTHING is the final DB-level safety net.
            var ledgerAffected = await conn.ExecuteAsync(
                @"INSERT INTO ledger_entries (id, funding_account_id, entry_type, amount, reference_id, created_at)
                  VALUES (uuid_generate_v4(), @FundingAccountId, @EntryType, @Amount, @ReferenceId, NOW())
                  ON CONFLICT (reference_id) DO NOTHING",
                new
                {
                    FundingAccountId = deposit.FundingAccountId,
                    EntryType = EntryType.Credit.ToString(),
                    Amount = settlementAmount,
                    ReferenceId = paymentId
                },
                transaction: tx);

            // Commit regardless of ledgerAffected:
            // if ledgerAffected == 0 the ledger entry already existed (pre-existing credit),
            // which means the customer already has their money. We still commit the deposit
            // status update (Completed) so the state is consistent. Log Critical for investigation.
            tx.Commit();

            if (ledgerAffected == 0)
            {
                // This should never happen in normal flow. Investigate: a ledger entry for this
                // payment existed before the deposit was marked Completed.
                // Financial state IS correct (credit exists + deposit now Completed).
                throw new InvalidOperationException(
                    $"INVESTIGATE: Ledger entry already existed for PaymentId={paymentId} before deposit was marked Completed. " +
                    $"DepositId={deposit.Id}. Financial state is correct but data inconsistency detected.");
            }
        }
        catch (InvalidOperationException)
        {
            // Re-throw inconsistency alerts (post-commit, tx already done) — do NOT attempt rollback.
            throw;
        }
        catch
        {
            try { tx.Rollback(); } catch { /* rollback best-effort */ }
            throw;
        }
    }

    public async Task MarkDepositFailedAsync(string paymentIntentId)
    {
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            @"UPDATE deposits SET status = @Status, updated_at = NOW()
              WHERE circle_payment_intent_id = @Id
                AND status NOT IN (@Completed, @Failed)",
            new
            {
                Status = DepositStatus.Failed.ToString(),
                Id = paymentIntentId,
                Completed = DepositStatus.Completed.ToString(),
                Failed = DepositStatus.Failed.ToString()
            });
    }

    public async Task MarkIntentCompleteAsync(string paymentIntentId)
    {
        // Payment intent complete fires after payments.paid has already set Completed.
        // Touch updated_at only — no credit, no status change.
        using var conn = _db.CreateConnection();
        await conn.ExecuteAsync(
            "UPDATE deposits SET updated_at = NOW() WHERE circle_payment_intent_id = @Id AND status = @Status",
            new { Id = paymentIntentId, Status = DepositStatus.Completed.ToString() });
    }
}
