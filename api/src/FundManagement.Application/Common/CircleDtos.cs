namespace FundManagement.Application.Common;

public record CirclePaymentIntentResponse(string Id, string Status, string? DepositAddress, string? Network);
public record CirclePayoutResponse(string Id, string Status);
public record CircleRecipientResponse(string Id, string Status, string? Chain, string? Address);
