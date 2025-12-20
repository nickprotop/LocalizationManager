namespace LrmCloud.Shared.DTOs.Admin;

/// <summary>
/// Webhook event for admin monitoring.
/// </summary>
public class AdminWebhookEventDto
{
    public int Id { get; set; }
    public string ProviderEventId { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string EventType { get; set; } = "";
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public DateTime ProcessedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
