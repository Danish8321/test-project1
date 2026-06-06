using FundManagement.Domain.Entities;

namespace FundManagement.Application.Webhooks;

public interface IWebhookService
{
    Task<IEnumerable<WebhookEvent>> GetAllAsync();
    Task ProcessAsync(string notificationId, string eventType, string payload);
}
