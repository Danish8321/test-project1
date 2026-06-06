using System.Text;
using FundManagement.Application.Webhooks;
using FundManagement.Infrastructure.Circle;
using Microsoft.AspNetCore.Mvc;

namespace FundManagement.Api.Controllers;

[ApiController]
[Route("webhooks")]
public class WebhooksController(IWebhookService svc, CircleSignatureValidator validator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await svc.GetAllAsync());

    [HttpPost("circle")]
    public async Task<IActionResult> ReceiveCircle()
    {
        // Must read raw body — never parse+re-serialize before verifying (whitespace breaks ECDSA sig)
        Request.EnableBuffering();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync();

        var signature = Request.Headers["X-Circle-Signature"].FirstOrDefault();
        var keyId = Request.Headers["X-Circle-Key-Id"].FirstOrDefault();

        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(keyId))
            return Unauthorized();

        if (!await validator.VerifyAsync(keyId, signature, rawBody))
            return Unauthorized();

        using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        var notificationType = root.GetProperty("notificationType").GetString()!;

        // Circle Mint format: no notificationId — use resource ID as idempotency key.
        // Resource is at top-level key matching the notificationType (e.g. "payout", "transfer").
        var resourceId = notificationType switch
        {
            "payouts" when root.TryGetProperty("payout", out var p)
                => p.TryGetProperty("id", out var pid) ? pid.GetString() : null,
            "transfers" when root.TryGetProperty("transfer", out var t)
                => t.TryGetProperty("id", out var tid) ? tid.GetString() : null,
            "addressBookRecipients" when root.TryGetProperty("addressBookRecipient", out var r)
                => r.TryGetProperty("id", out var rid) ? rid.GetString() : null,
            _ => null
        } ?? $"{notificationType}:{(root.TryGetProperty("clientId", out var cid) ? cid.GetString() : Guid.NewGuid().ToString())}";

        await svc.ProcessAsync(resourceId, notificationType, rawBody);
        return Ok();
    }
}
