using FundManagement.Domain.Entities;

namespace FundManagement.Application.Deposits;

public interface IDepositService
{
    Task<IEnumerable<Deposit>> GetAllAsync();
    Task<IEnumerable<Deposit>> GetByCustomerAsync(Guid customerId);
    Task<Deposit?> GetByIdAsync(Guid id);
    Task<Deposit> CreateAsync(Guid customerId, Guid fundingAccountId, decimal amount);
    Task ProcessSettlementAsync(string circlePaymentIntentId, string circleStatus);
}
