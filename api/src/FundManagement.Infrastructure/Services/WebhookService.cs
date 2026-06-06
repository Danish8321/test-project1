using System.Text.Json;
using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Deposits;
using FundManagement.Application.Webhooks;
using FundManagement.Application.Withdrawals;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;

namespace FundManagement.Infrastructure.Services;

public class WebhookService : IWebhookService
{
    private readonly IDbConnectionFactory _db;
    private readonly IDepositService _deposits;
    private readonly IWithdrawalService _withdrawals;

    public WebhookService(IDbConnectionFactory db, IDepositService deposits, IWithdrawalService withdrawals)
    {
        _db = db;
        _deposits = deposits;
        _withdrawals = withdrawals;
    }

    public async Task<IEnumerable<WebhookEvent>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<WebhookEvent>(
            "SELECT * FROM webhook_events ORDER BY created_at DESC");
    }

    public async Task ProcessAsync(string notificationId, string eventType, string payload)
    {
        using var conn = _db.CreateConnection();

        var affected = await conn.ExecuteAsync(
            @"INSERT INTO webhook_events
                (id, circle_event_id, event_type, payload, status, created_at)
              VALUES
                (uuid_generate_v4(), @EventId, @EventType, @Payload::jsonb, @Status, NOW())
              ON CONFLICT (circle_event_id) DO NOTHING",
            new { EventId = notificationId, EventType = eventType, Payload = payload, Status = WebhookStatus.Received.ToString() });

        if (affected == 0) return;

        try
        {
            await DispatchAsync(eventType, payload);

            await conn.ExecuteAsync(
                "UPDATE webhook_events SET status = @Status, processed_at = NOW() WHERE circle_event_id = @EventId",
                new { Status = WebhookStatus.Processed.ToString(), EventId = notificationId });
        }
        catch
        {
            await conn.ExecuteAsync(
                "UPDATE webhook_events SET status = @Status WHERE circle_event_id = @EventId",
                new { Status = WebhookStatus.Failed.ToString(), EventId = notificationId });
            throw;
        }
    }

    private async Task DispatchAsync(string eventType, string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;

        switch (eventType)
        {
            // Circle Mint: payout status change
            case "payouts":
                if (!root.TryGetProperty("payout", out var payout)) return;
                var payoutId = payout.GetProperty("id").GetString()!;
                var payoutStatus = payout.GetProperty("status").GetString()!;
                if (payoutStatus is "complete" or "failed")
                    await _withdrawals.ProcessPayoutSettlementAsync(payoutId, payoutStatus);
                break;

            // Circle Mint: inbound blockchain transfer (deposit settlement)
            case "transfers":
                if (!root.TryGetProperty("transfer", out var transfer)) return;
                var transferStatus = transfer.GetProperty("status").GetString()!;
                if (transferStatus is not ("complete" or "failed")) return;

                // Only handle inbound (blockchain → Circle wallet)
                if (!transfer.TryGetProperty("source", out var src)) return;
                if (src.TryGetProperty("type", out var srcType) && srcType.GetString() != "blockchain") return;

                // Circle may include paymentIntentId on the transfer when created via a payment intent
                if (transfer.TryGetProperty("paymentIntentId", out var piIdProp))
                {
                    var piId = piIdProp.GetString();
                    if (piId != null)
                        await _deposits.ProcessSettlementAsync(piId, transferStatus == "complete" ? "complete" : "failed");
                }
                break;

            // All other notificationTypes (addressBookRecipients, etc.): log and ignore
        }
    }
}
