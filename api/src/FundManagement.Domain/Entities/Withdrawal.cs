using FundManagement.Domain.Enums;

namespace FundManagement.Domain.Entities;

public class Withdrawal
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid FundingAccountId { get; set; }
    public string CirclePayoutId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public WithdrawalStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
