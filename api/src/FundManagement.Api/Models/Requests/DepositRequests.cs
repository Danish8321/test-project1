namespace FundManagement.Api.Models.Requests;

public record CreateDepositRequest(Guid CustomerId, Guid FundingAccountId, decimal Amount);
