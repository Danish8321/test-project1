using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Reconciliation;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class ReconciliationService : IReconciliationService
{
    private readonly IDbConnectionFactory _db;
    private readonly ICircleClient _circle;

    public ReconciliationService(IDbConnectionFactory db, ICircleClient circle)
    {
        _db = db;
        _circle = circle;
    }

    public async Task<ReconciliationResult> RunAsync()
    {
        using var conn = _db.CreateConnection();
        var deposits = (await conn.QueryAsync<Deposit>("SELECT * FROM deposits")).ToList();
        var withdrawals = (await conn.QueryAsync<Withdrawal>("SELECT * FROM withdrawals")).ToList();

        var mismatches = new List<string>();
        int matchedDeposits = 0, unmatchedDeposits = 0;

        foreach (var deposit in deposits.Where(d => d.CirclePaymentIntentId != string.Empty))
        {
            var circle = await _circle.GetPaymentIntentAsync(deposit.CirclePaymentIntentId);
            var expected = circle.Status == "complete" ? DepositStatus.Completed
                         : circle.Status == "failed" ? DepositStatus.Failed
                         : DepositStatus.Pending;

            if (deposit.Status == expected)
                matchedDeposits++;
            else
            {
                unmatchedDeposits++;
                mismatches.Add($"Deposit {deposit.Id}: local={deposit.Status} circle={circle.Status}");
            }
        }

        int matchedWithdrawals = 0, unmatchedWithdrawals = 0;

        foreach (var withdrawal in withdrawals.Where(w => w.CirclePayoutId != string.Empty))
        {
            var circle = await _circle.GetPayoutAsync(withdrawal.CirclePayoutId);
            var expected = circle.Status == "complete" ? WithdrawalStatus.Completed
                         : circle.Status == "failed" ? WithdrawalStatus.Failed
                         : WithdrawalStatus.Pending;

            if (withdrawal.Status == expected)
                matchedWithdrawals++;
            else
            {
                unmatchedWithdrawals++;
                mismatches.Add($"Withdrawal {withdrawal.Id}: local={withdrawal.Status} circle={circle.Status}");
            }
        }

        return new ReconciliationResult(
            DateTimeOffset.UtcNow,
            deposits.Count,
            matchedDeposits,
            unmatchedDeposits,
            withdrawals.Count,
            matchedWithdrawals,
            unmatchedWithdrawals,
            mismatches);
    }
}
