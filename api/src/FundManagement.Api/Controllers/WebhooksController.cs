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

        var notificationId = root.GetProperty("notificationId").GetString()!;
        var notificationType = root.GetProperty("notificationType").GetString()!;

        await svc.ProcessAsync(notificationId, notificationType, rawBody);
        return Ok();
    }
}
