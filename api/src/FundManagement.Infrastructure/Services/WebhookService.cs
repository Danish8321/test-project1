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

    public async Task ProcessAsync(string circleEventId, string eventType, string payload)
    {
        using var conn = _db.CreateConnection();

        var affected = await conn.ExecuteAsync(
            @"INSERT INTO webhook_events
                (id, circle_event_id, event_type, payload, status, created_at)
              VALUES
                (uuid_generate_v4(), @EventId, @EventType, @Payload::jsonb, @Status, NOW())
              ON CONFLICT (circle_event_id) DO NOTHING",
            new { EventId = circleEventId, EventType = eventType, Payload = payload, Status = WebhookStatus.Received.ToString() });

        if (affected == 0) return;

        try
        {
            await DispatchAsync(eventType, payload);

            await conn.ExecuteAsync(
                "UPDATE webhook_events SET status = @Status, processed_at = NOW() WHERE circle_event_id = @EventId",
                new { Status = WebhookStatus.Processed.ToString(), EventId = circleEventId });
        }
        catch
        {
            await conn.ExecuteAsync(
                "UPDATE webhook_events SET status = @Status WHERE circle_event_id = @EventId",
                new { Status = WebhookStatus.Failed.ToString(), EventId = circleEventId });
            throw;
        }
    }

    private async Task DispatchAsync(string eventType, string payload)
    {
        using var doc = JsonDocument.Parse(payload);
        switch (eventType)
        {
            case "payments.payment_intent.completed":
                if (doc.RootElement.TryGetProperty("paymentIntentId", out var pId1))
                    await _deposits.ProcessSettlementAsync(pId1.GetString()!, "complete");
                break;
            case "payments.payment_intent.failed":
                if (doc.RootElement.TryGetProperty("paymentIntentId", out var pId2))
                    await _deposits.ProcessSettlementAsync(pId2.GetString()!, "failed");
                break;
            case "payouts.payout.complete":
                if (doc.RootElement.TryGetProperty("payoutId", out var pId3))
                    await _withdrawals.ProcessPayoutSettlementAsync(pId3.GetString()!, "complete");
                break;
            case "payouts.payout.failed":
                if (doc.RootElement.TryGetProperty("payoutId", out var pId4))
                    await _withdrawals.ProcessPayoutSettlementAsync(pId4.GetString()!, "failed");
                break;
        }
    }
}
