using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Ledger;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class LedgerService : ILedgerService
{
    private readonly IDbConnectionFactory _db;

    public LedgerService(IDbConnectionFactory db) => _db = db;

    public async Task<IEnumerable<LedgerEntry>> GetByFundingAccountAsync(Guid fundingAccountId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<LedgerEntry>(
            "SELECT * FROM ledger_entries WHERE funding_account_id = @FundingAccountId ORDER BY created_at ASC",
            new { FundingAccountId = fundingAccountId });
    }

    public async Task<decimal> GetBalanceAsync(Guid fundingAccountId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<decimal>(
            @"SELECT COALESCE(
                SUM(CASE WHEN entry_type = 'Credit' THEN amount ELSE -amount END),
              0)
              FROM ledger_entries
              WHERE funding_account_id = @FundingAccountId",
            new { FundingAccountId = fundingAccountId });
    }

    public async Task<LedgerEntry> CreateEntryAsync(
        Guid fundingAccountId, EntryType entryType, decimal amount, string referenceId)
    {
        using var conn = _db.CreateConnection();
        return await conn.QuerySingleAsync<LedgerEntry>(
            @"INSERT INTO ledger_entries (id, funding_account_id, entry_type, amount, reference_id, created_at)
              VALUES (uuid_generate_v4(), @FundingAccountId, @EntryType, @Amount, @ReferenceId, NOW())
              RETURNING *",
            new { FundingAccountId = fundingAccountId, EntryType = entryType.ToString(), Amount = amount, ReferenceId = referenceId });
    }
}
