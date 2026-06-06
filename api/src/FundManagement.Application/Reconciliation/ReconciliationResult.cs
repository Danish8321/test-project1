namespace FundManagement.Application.Reconciliation;

public record ReconciliationResult(
    DateTimeOffset RunAt,
    int TotalDeposits,
    int MatchedDeposits,
    int UnmatchedDeposits,
    int TotalWithdrawals,
    int MatchedWithdrawals,
    int UnmatchedWithdrawals,
    IReadOnlyList<string> Mismatches);
