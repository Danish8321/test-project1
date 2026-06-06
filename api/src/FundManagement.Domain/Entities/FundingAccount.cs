namespace FundManagement.Domain.Entities;

public class FundingAccount
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public string Currency { get; set; } = "USDC";
    public DateTime CreatedAt { get; set; }
}
