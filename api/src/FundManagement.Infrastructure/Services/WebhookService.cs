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

        // Circle places the resource ID at notification.id — never root level
        if (!doc.RootElement.TryGetProperty("notification", out var notification))
            return;

        if (!notification.TryGetProperty("id", out var idProp))
            return;

        var resourceId = idProp.GetString();
        if (resourceId == null) return;

        switch (eventType)
        {
            case "payments.payment_intent.completed":
                await _deposits.ProcessSettlementAsync(resourceId, "complete");
                break;
            case "payments.payment_intent.failed":
                await _deposits.ProcessSettlementAsync(resourceId, "failed");
                break;
            case "payouts.payout.complete":
                await _withdrawals.ProcessPayoutSettlementAsync(resourceId, "complete");
                break;
            case "payouts.payout.failed":
                await _withdrawals.ProcessPayoutSettlementAsync(resourceId, "failed");
                break;
            // All other notificationTypes: log and ignore — do not error
        }
    }
}
