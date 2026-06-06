namespace FundManagement.Application.Common;

public interface ICircleClient
{
    Task<bool> PingAsync(CancellationToken ct = default);
    Task<CirclePaymentIntentResponse> CreatePaymentIntentAsync(decimal amount, string currency, string idempotencyKey, CancellationToken ct = default);
    Task<CirclePaymentIntentResponse> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct = default);
    Task<CirclePayoutResponse> CreatePayoutAsync(decimal amount, string currency, string destinationAddress, string idempotencyKey, CancellationToken ct = default);
    Task<CirclePayoutResponse> GetPayoutAsync(string payoutId, CancellationToken ct = default);
}
