using FundManagement.Domain.Enums;

namespace FundManagement.Domain.Entities;

public class LedgerEntry
{
    public Guid Id { get; set; }
    public Guid FundingAccountId { get; set; }
    public EntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public string ReferenceId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
