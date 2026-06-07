using System.Text.Json;
using Dapper;
using FundManagement.Application.Common;
using FundManagement.Application.Deposits;
using FundManagement.Application.Webhooks;
using FundManagement.Application.Withdrawals;
using FundManagement.Domain.Entities;
using FundManagement.Domain.Enums;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace FundManagement.Infrastructure.Services;

public class WebhookService : IWebhookService
{
    private static readonly DistributedCacheEntryOptions CacheTtl =
        new() { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(48) };

    private readonly IDbConnectionFactory _db;
    private readonly IDepositService _deposits;
    private readonly IWithdrawalService _withdrawals;
    private readonly IDistributedCache _cache;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        IDbConnectionFactory db,
        IDepositService deposits,
        IWithdrawalService withdrawals,
        IDistributedCache cache,
        ILogger<WebhookService> logger)
    {
        _db = db;
        _deposits = deposits;
        _withdrawals = withdrawals;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IEnumerable<WebhookEvent>> GetAllAsync()
    {
        using var conn = _db.CreateConnection();
        return await conn.QueryAsync<WebhookEvent>(
            "SELECT * FROM webhook_events ORDER BY created_at DESC");
    }

    public async Task ProcessAsync(string notificationId, string eventType, string payload)
    {
        using var _ = LogContext.PushProperty("WebhookResourceId", notificationId);
        using var __ = LogContext.PushProperty("WebhookEventType", eventType);

        // Fast-path: Redis check before any DB access.
        // Wrapped in try/catch — Redis failure must never block webhook processing.
        var cacheKey = $"wh:{notificationId}";
        try
        {
            var cached = await _cache.GetAsync(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation("Webhook duplicate skipped via cache. ResourceId={ResourceId} EventType={EventType}", notificationId, eventType);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable for idempotency check — falling through to DB. ResourceId={ResourceId}", notificationId);
        }

        using var conn = _db.CreateConnection();

        var affected = await conn.ExecuteAsync(
            @"INSERT INTO webhook_events
                (id, circle_event_id, event_type, payload, status, created_at)
              VALUES
                (uuid_generate_v4(), @EventId, @EventType, @Payload::jsonb, @Status, NOW())
              ON CONFLICT (circle_event_id) DO NOTHING",
            new { EventId = notificationId, EventType = eventType, Payload = payload, Status = WebhookStatus.Received.ToString() });

        if (affected == 0)
        {
            _logger.LogInformation("Webhook duplicate skipped via DB constraint. ResourceId={ResourceId} EventType={EventType}", notificationId, eventType);
            try { await _cache.SetAsync(cacheKey, [1], CacheTtl); }
            catch (Exception ex) { _logger.LogWarning(ex, "Redis backfill failed (non-fatal). ResourceId={ResourceId}", notificationId); }
            return;
        }

        try
        {
            _logger.LogInformation("Dispatching webhook. ResourceId={ResourceId} EventType={EventType}", notificationId, eventType);
            await DispatchAsync(eventType, payload);

            await conn.ExecuteAsync(
                "UPDATE webhook_events SET status = @Status, processed_at = NOW() WHERE circle_event_id = @EventId",
                new { Status = WebhookStatus.Processed.ToString(), EventId = notificationId });

            try { await _cache.SetAsync(cacheKey, [1], CacheTtl); }
            catch (Exception ex) { _logger.LogWarning(ex, "Redis write failed after processing (non-fatal). ResourceId={ResourceId}", notificationId); }

            _logger.LogInformation("Webhook processed. ResourceId={ResourceId} EventType={EventType}", notificationId, eventType);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex,
                "Webhook dispatch FAILED — manual review required. ResourceId={ResourceId} EventType={EventType}",
                notificationId, eventType);
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
            case "payments":
                await HandlePaymentAsync(root);
                break;

            case "paymentIntents":
                await HandlePaymentIntentAsync(root);
                break;

            case "payouts":
                if (!root.TryGetProperty("payout", out var payout)) return;
                var payoutId = payout.GetProperty("id").GetString()!;
                var payoutStatus = payout.GetProperty("status").GetString()!;
                if (payoutStatus is "complete" or "failed")
                    await _withdrawals.ProcessPayoutSettlementAsync(payoutId, payoutStatus);
                break;
        }
    }

    private async Task HandlePaymentAsync(JsonElement root)
    {
        if (!root.TryGetProperty("payment", out var payment)) return;

        var paymentId = payment.GetProperty("id").GetString()!;
        var status = payment.GetProperty("status").GetString()!;

        if (!payment.TryGetProperty("paymentIntentId", out var piProp)) return;
        var paymentIntentId = piProp.GetString();
        if (paymentIntentId == null) return;

        switch (status)
        {
            case "pending":
                var txHash = payment.TryGetProperty("transactionHash", out var txProp) ? txProp.GetString() : null;
                await _deposits.MarkPaymentDetectedAsync(paymentIntentId, paymentId, txHash);
                break;

            case "paid":
                var settlementEl = payment.TryGetProperty("settlementAmount", out var saProp)
                    ? saProp
                    : payment.GetProperty("amount");
                var amount = settlementEl.GetProperty("amount").GetDecimal();
                await _deposits.ProcessSettlementByPaymentAsync(paymentId, paymentIntentId, amount);
                break;

            case "failed":
            case "cancelled":
                await _deposits.MarkDepositFailedAsync(paymentIntentId);
                break;
        }
    }

    private async Task HandlePaymentIntentAsync(JsonElement root)
    {
        if (!root.TryGetProperty("paymentIntent", out var intent)) return;

        var intentId = intent.GetProperty("id").GetString()!;
        var latestStatus = GetLatestTimelineStatus(intent);

        switch (latestStatus)
        {
            case "pending":
                string? address = null;
                string? chain = null;
                if (intent.TryGetProperty("paymentMethods", out var methods) &&
                    methods.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in methods.EnumerateArray())
                    {
                        chain = m.TryGetProperty("chain", out var c) ? c.GetString() : null;
                        address = m.TryGetProperty("address", out var a) ? a.GetString() : null;
                        break;
                    }
                }
                DateTimeOffset? expiresOn = null;
                if (intent.TryGetProperty("expiresOn", out var expProp) &&
                    DateTimeOffset.TryParse(expProp.GetString(), out var parsed))
                    expiresOn = parsed;

                await _deposits.UpdateDepositAddressAsync(intentId, address, chain, expiresOn);
                break;

            case "complete":
                await _deposits.MarkIntentCompleteAsync(intentId);
                break;

            // "active" = continuous intent still open — credit handled by payments.paid
            // "created" = intent just registered, address not assigned yet
        }
    }

    private static string? GetLatestTimelineStatus(JsonElement intent)
    {
        if (!intent.TryGetProperty("timeline", out var timeline) ||
            timeline.ValueKind != JsonValueKind.Array)
            return null;

        string? latestStatus = null;
        DateTimeOffset latestTime = DateTimeOffset.MinValue;

        foreach (var entry in timeline.EnumerateArray())
        {
            if (!entry.TryGetProperty("time", out var timeProp)) continue;
            if (!DateTimeOffset.TryParse(timeProp.GetString(), out var t)) continue;
            if (t > latestTime)
            {
                latestTime = t;
                latestStatus = entry.TryGetProperty("status", out var s) ? s.GetString() : null;
            }
        }

        return latestStatus;
    }
}
