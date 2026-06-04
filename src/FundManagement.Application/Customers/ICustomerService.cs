using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Application.Customers;

public interface ICustomerService
{
    Task<IEnumerable<Customer>> GetAllAsync();
    Task<Customer?> GetByIdAsync(Guid id);
    Task<Customer> CreateAsync(string name, string email, CustomerType type);
    Task<IEnumerable<FundingAccount>> GetFundingAccountsAsync(Guid customerId);
    Task<FundingAccount> CreateFundingAccountAsync(Guid customerId, string currency);
}
