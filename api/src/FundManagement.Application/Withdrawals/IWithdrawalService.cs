using FundManagement.Domain.Entities;

namespace FundManagement.Application.Withdrawals;

public interface IWithdrawalService
{
    Task<IEnumerable<Withdrawal>> GetAllAsync();
    Task<IEnumerable<Withdrawal>> GetByCustomerAsync(Guid customerId);
    Task<Withdrawal?> GetByIdAsync(Guid id);
    Task<Withdrawal> CreateAsync(Guid customerId, Guid fundingAccountId, decimal amount, string destinationAddress);
    Task ProcessPayoutSettlementAsync(string circlePayoutId, string circleStatus);
}
