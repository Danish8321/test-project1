using System.Text;
using System.Text.Json;
using FundManagement.Application.Webhooks;
using FundManagement.Infrastructure.Circle;
using Microsoft.AspNetCore.Mvc;
using Serilog.Context;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController(
    IWebhookService svc,
    CircleSignatureValidator validator,
    IHttpClientFactory httpClientFactory,
    ILogger<WebhooksController> logger) : ControllerBase
{
    [HttpHead("circle")]
    public IActionResult Head() => Ok();

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await svc.GetAllAsync());

    [HttpPost("circle")]
    public async Task<IActionResult> ReceiveCircle(CancellationToken ct)
    {
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrWhiteSpace(rawBody))
            return BadRequest("Empty payload.");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(rawBody); }
        catch { return BadRequest("Invalid JSON."); }

        using (doc)
        {
            var root = doc.RootElement;

            // Detect SNS vs direct Circle: SNS always has a "Type" field
            if (root.TryGetProperty("Type", out var typeProp))
                return await HandleSnsEnvelope(root, typeProp.GetString(), ct);

            return await HandleDirectCircle(root, rawBody, ct);
        }
    }

    // ── SNS path ──────────────────────────────────────────────────────────────

    private async Task<IActionResult> HandleSnsEnvelope(JsonElement root, string? snsType, CancellationToken ct)
    {
        switch (snsType)
        {
            case "SubscriptionConfirmation":
                return await HandleSubscriptionConfirmation(root, ct);

            case "Notification":
                return await HandleSnsNotification(root, ct);

            case "UnsubscribeConfirmation":
                logger.LogWarning("SNS UnsubscribeConfirmation received.");
                return Ok();

            default:
                logger.LogWarning("Unknown SNS Type={SnsType}", snsType);
                return Ok();
        }
    }

    private async Task<IActionResult> HandleSubscriptionConfirmation(JsonElement root, CancellationToken ct)
    {
        // Validate cert host before calling the URL
        var certUrl = TryGetString(root, "SigningCertURL");
        if (certUrl == null || !Uri.TryCreate(certUrl, UriKind.Absolute, out var certUri) ||
            !certUri.Host.EndsWith("amazonaws.com", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("SNS SubscriptionConfirmation rejected: invalid SigningCertURL={Url}", certUrl);
            return Unauthorized();
        }

        var subscribeUrl = TryGetString(root, "SubscribeURL");
        if (string.IsNullOrWhiteSpace(subscribeUrl))
            return BadRequest("SubscribeURL missing.");

        var messageId = TryGetString(root, "MessageId") ?? Guid.NewGuid().ToString("N");

        // Call SubscribeURL BEFORE storing — if URL call fails, SNS retries and we try again.
        // Storing first would mark the event as processed and block all retries.
        var client = httpClientFactory.CreateClient();
        var response = await client.GetAsync(subscribeUrl, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("SNS subscription confirmation HTTP failed. Status={Status}", response.StatusCode);
            return StatusCode(500);
        }

        await svc.ProcessAsync(messageId, "SubscriptionConfirmation", root.GetRawText());

        logger.LogInformation("SNS subscription confirmed. MessageId={MessageId}", messageId);
        return Ok();
    }

    private async Task<IActionResult> HandleSnsNotification(JsonElement root, CancellationToken ct)
    {
        var snsMessageId = TryGetString(root, "MessageId");
        if (string.IsNullOrWhiteSpace(snsMessageId))
            return BadRequest("SNS MessageId missing.");

        var message = TryGetString(root, "Message");
        if (string.IsNullOrWhiteSpace(message))
            return BadRequest("SNS Message missing.");

        await ProcessCircleNotification(snsMessageId, message);
        return Ok();
    }

    // ── Direct Circle signed path (non-SNS) ───────────────────────────────────

    private async Task<IActionResult> HandleDirectCircle(JsonElement root, string rawBody, CancellationToken ct)
    {
        var signature = Request.Headers["X-Circle-Signature"].FirstOrDefault();
        var keyId = Request.Headers["X-Circle-Key-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(keyId))
            return Unauthorized();

        if (!await validator.VerifyAsync(keyId, signature, rawBody, ct))
            return Unauthorized();

        await ProcessCircleNotification(snsMessageId: null, message: rawBody);
        return Ok();
    }

    // ── Shared inner-notification processor ──────────────────────────────────

    private async Task ProcessCircleNotification(string? snsMessageId, string message)
    {
        using var doc = JsonDocument.Parse(message);
        var root = doc.RootElement;

        var notificationType = TryGetString(root, "notificationType");
        var clientId = TryGetString(root, "clientId");

        var resourceId = notificationType switch
        {
            // payments.pending and payments.paid have the SAME payment.id — must include status
            // to avoid payments.paid being treated as duplicate of payments.pending
            "payments" when root.TryGetProperty("payment", out var p)
                => BuildStatusedResourceId(p),
            // paymentIntents fires multiple events per intent — include latest timeline status
            "paymentIntents" when root.TryGetProperty("paymentIntent", out var pi)
                => BuildPaymentIntentResourceId(pi),
            // payouts: each payout id is unique per status progression — include status
            "payouts" when root.TryGetProperty("payout", out var po)
                => BuildStatusedResourceId(po),
            _ => null
        } ?? snsMessageId ?? $"{notificationType}:{clientId}:{Guid.NewGuid():N}";

        using (LogContext.PushProperty("WebhookNotificationType", notificationType ?? "unknown"))
        using (LogContext.PushProperty("WebhookResourceId", resourceId))
        using (LogContext.PushProperty("WebhookSnsMessageId", snsMessageId))
        using (LogContext.PushProperty("CircleClientId", clientId))
        {
            logger.LogInformation("Circle notification received. NotificationType={NotificationType} ResourceId={ResourceId}", notificationType, resourceId);
            await svc.ProcessAsync(resourceId, notificationType ?? "unknown", message);
        }
    }

    // Combines resource id + status so each status transition is a distinct idempotency key.
    // Prevents payments.pending consuming the key and blocking payments.paid.
    private static string? BuildStatusedResourceId(JsonElement resource)
    {
        var id = TryGetString(resource, "id");
        if (id == null) return null;
        var status = TryGetString(resource, "status");
        return status != null ? $"{id}:{status}" : id;
    }

    private static string? BuildPaymentIntentResourceId(JsonElement intent)
    {
        var id = TryGetString(intent, "id");
        if (id == null) return null;

        // Include latest timeline status so each status transition gets its own webhook_events row
        string? latestStatus = null;
        DateTimeOffset latestTime = DateTimeOffset.MinValue;
        if (intent.TryGetProperty("timeline", out var tl) && tl.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in tl.EnumerateArray())
            {
                if (!entry.TryGetProperty("time", out var tp)) continue;
                if (!DateTimeOffset.TryParse(tp.GetString(), out var t)) continue;
                if (t > latestTime)
                {
                    latestTime = t;
                    latestStatus = TryGetString(entry, "status");
                }
            }
        }

        return latestStatus != null ? $"{id}:{latestStatus}" : id;
    }

    private static string? TryGetString(JsonElement el, string key) =>
        el.TryGetProperty(key, out var p) && p.ValueKind != JsonValueKind.Null
            ? p.GetString()
            : null;
}
