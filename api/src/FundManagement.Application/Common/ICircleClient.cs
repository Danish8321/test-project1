namespace FundManagement.Application.Common;

public interface ICircleClient
{
    Task<bool> PingAsync(CancellationToken ct = default);

    // Crypto Deposits API (Payment Intents)
    Task<CirclePaymentIntentResponse> CreatePaymentIntentAsync(decimal amount, string currency, string idempotencyKey, CancellationToken ct = default);
    Task<CirclePaymentIntentResponse> GetPaymentIntentAsync(string paymentIntentId, CancellationToken ct = default);

    // Crypto Payouts API — Address Book (Sep 2025 relaunch: /v1/addressBook/recipients)
    Task<CircleRecipientResponse> CreateRecipientAsync(string address, string chain, string idempotencyKey, CancellationToken ct = default);
    Task<CircleRecipientResponse> GetRecipientAsync(string recipientId, CancellationToken ct = default);

    // Crypto Payouts API — Payouts (Sep 2025 relaunch: /v1/payouts)
    Task<CirclePayoutResponse> CreatePayoutAsync(decimal amount, string currency, string recipientId, string idempotencyKey, CancellationToken ct = default);
    Task<CirclePayoutResponse> GetPayoutAsync(string payoutId, CancellationToken ct = default);
}
