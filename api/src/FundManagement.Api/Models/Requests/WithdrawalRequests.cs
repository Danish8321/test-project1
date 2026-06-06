namespace FundManagement.Api.Models.Requests;

public record CreateWithdrawalRequest(Guid CustomerId, Guid FundingAccountId, decimal Amount, string DestinationAddress);
