using System.Text;
using FundManagement.Application.Webhooks;
using FundManagement.Infrastructure.Circle;

namespace FundManagement.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/webhooks").WithTags("Webhooks");

        g.MapGet("/", async (IWebhookService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        g.MapPost("/circle", async (HttpContext ctx, CircleSignatureValidator validator, IWebhookService svc) =>
        {
            // Must read raw body — never parse+re-serialize before verifying (whitespace breaks ECDSA sig)
            ctx.Request.EnableBuffering();
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();

            var signature = ctx.Request.Headers["X-Circle-Signature"].FirstOrDefault();
            var keyId = ctx.Request.Headers["X-Circle-Key-Id"].FirstOrDefault();

            if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(keyId))
                return Results.Unauthorized();

            if (!await validator.VerifyAsync(keyId, signature, rawBody))
                return Results.Unauthorized();

            using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            var notificationId = root.GetProperty("notificationId").GetString()!;
            var notificationType = root.GetProperty("notificationType").GetString()!;

            await svc.ProcessAsync(notificationId, notificationType, rawBody);
            return Results.Ok();
        });
    }
}
