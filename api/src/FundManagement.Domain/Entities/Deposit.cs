using FundManagement.Domain.Enums;

namespace FundManagement.Domain.Entities;

public class Deposit
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid FundingAccountId { get; set; }
    public string CirclePaymentIntentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DepositStatus Status { get; set; }
    public string? CirclePaymentId { get; set; }
    public string? DepositAddress { get; set; }
    public string? Chain { get; set; }
    public DateTimeOffset? ExpiresOn { get; set; }
    public string? TransactionHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
