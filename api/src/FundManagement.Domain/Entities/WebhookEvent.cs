using FundManagement.Domain.Enums;

namespace FundManagement.Domain.Entities;

public class WebhookEvent
{
    public Guid Id { get; set; }
    public string CircleEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public WebhookStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
