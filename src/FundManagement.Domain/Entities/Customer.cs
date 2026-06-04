using FundManagement.Domain.Enums;

namespace FundManagement.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public CustomerType CustomerType { get; set; }
    public DateTime CreatedAt { get; set; }
}
