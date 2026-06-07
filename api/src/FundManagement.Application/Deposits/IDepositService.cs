using FundManagement.Domain.Entities;

namespace FundManagement.Application.Deposits;

public interface IDepositService
{
    Task<IEnumerable<Deposit>> GetAllAsync();
    Task<IEnumerable<Deposit>> GetByCustomerAsync(Guid customerId);
    Task<Deposit?> GetByIdAsync(Guid id);
    Task<Deposit> CreateAsync(Guid customerId, Guid fundingAccountId, decimal amount);
    Task UpdateDepositAddressAsync(string paymentIntentId, string? address, string? chain, DateTimeOffset? expiresOn);
    Task MarkPaymentDetectedAsync(string paymentIntentId, string paymentId, string? transactionHash);
    Task ProcessSettlementByPaymentAsync(string paymentId, string paymentIntentId, decimal settlementAmount);
    Task MarkDepositFailedAsync(string paymentIntentId);
    Task MarkIntentCompleteAsync(string paymentIntentId);
}
