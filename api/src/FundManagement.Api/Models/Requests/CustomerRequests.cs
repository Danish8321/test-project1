using FundManagement.Domain.Enums;

namespace FundManagement.Api.Models.Requests;

public record CreateCustomerRequest(string Name, string Email, CustomerType CustomerType);
public record CreateFundingAccountRequest(string Currency);
