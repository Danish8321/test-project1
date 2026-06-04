using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Application.Ledger;

public interface ILedgerService
{
    Task<IEnumerable<LedgerEntry>> GetByFundingAccountAsync(Guid fundingAccountId);
    Task<decimal> GetBalanceAsync(Guid fundingAccountId);
    Task<LedgerEntry> CreateEntryAsync(Guid fundingAccountId, EntryType entryType, decimal amount, string referenceId);
}
