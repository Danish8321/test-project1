using FundManagement.Application.Webhooks;

namespace FundManagement.Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this WebApplication app)
    {
        var g = app.MapGroup("/webhooks").WithTags("Webhooks");

        g.MapGet("/", async (IWebhookService svc) =>
            Results.Ok(await svc.GetAllAsync()));

        g.MapPost("/circle", async (CircleWebhookRequest req, IWebhookService svc) =>
        {
            await svc.ProcessAsync(req.EventId, req.EventType, req.Payload);
            return Results.Ok();
        });
    }
}

public record CircleWebhookRequest(string EventId, string EventType, string Payload);
