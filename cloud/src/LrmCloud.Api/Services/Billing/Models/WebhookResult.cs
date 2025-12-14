namespace LrmCloud.Api.Services.Billing.Models;

/// <summary>
/// Result of processing a webhook from a payment provider.
/// </summary>
public sealed class WebhookResult
{
    /// <summary>
    /// Whether the webhook was processed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The type of event that was processed.
    /// </summary>
    public required WebhookEventType EventType { get; init; }

    /// <summary>
    /// The payment provider that sent the webhook.
    /// </summary>
    public required string ProviderName { get; init; }

    /// <summary>
    /// The customer ID associated with this event (if applicable).
    /// </summary>
    public string? CustomerId { get; init; }

    /// <summary>
    /// The subscription ID associated with this event (if applicable).
    /// </summary>
    public string? SubscriptionId { get; init; }

    /// <summary>
    /// The plan associated with this event (if applicable).
    /// </summary>
    public string? Plan { get; init; }

    /// <summary>
    /// Updated subscription status (if applicable).
    /// </summary>
    public SubscriptionStatus? NewStatus { get; init; }

    /// <summary>
    /// When the current period ends (if applicable).
    /// </summary>
    public DateTime? CurrentPeriodEnd { get; init; }

    /// <summary>
    /// Whether the subscription will cancel at period end (if applicable).
    /// </summary>
    public bool? CancelAtPeriodEnd { get; init; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Raw event ID from the provider for logging/debugging.
    /// </summary>
    public string? ProviderEventId { get; init; }

    /// <summary>
    /// Creates a successful webhook result.
    /// </summary>
    public static WebhookResult Succeeded(string providerName, WebhookEventType eventType) => new()
    {
        Success = true,
        ProviderName = providerName,
        EventType = eventType
    };

    /// <summary>
    /// Creates a failed webhook result.
    /// </summary>
    public static WebhookResult Failed(string providerName, string errorMessage) => new()
    {
        Success = false,
        ProviderName = providerName,
        EventType = WebhookEventType.Unknown,
        ErrorMessage = errorMessage
    };
}
