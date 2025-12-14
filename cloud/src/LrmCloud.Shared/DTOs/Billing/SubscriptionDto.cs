namespace LrmCloud.Shared.DTOs.Billing;

/// <summary>
/// Represents a user's subscription status.
/// </summary>
public record SubscriptionDto
{
    /// <summary>
    /// Current plan: "free", "team", or "enterprise".
    /// </summary>
    public string Plan { get; init; } = "free";

    /// <summary>
    /// Subscription status: "none", "active", "past_due", "canceled", "incomplete".
    /// </summary>
    public string Status { get; init; } = "none";

    /// <summary>
    /// End of current billing period (when subscription renews or ends).
    /// </summary>
    public DateTime? CurrentPeriodEnd { get; init; }

    /// <summary>
    /// Whether the subscription will cancel at period end.
    /// </summary>
    public bool CancelAtPeriodEnd { get; init; }

    /// <summary>
    /// Payment provider subscription ID.
    /// </summary>
    public string? PaymentSubscriptionId { get; init; }

    /// <summary>
    /// Active payment provider: "stripe", "paypal", etc.
    /// </summary>
    public string? PaymentProvider { get; init; }

    /// <summary>
    /// Whether the user has an active paid subscription.
    /// </summary>
    public bool IsActive => Status == "active" && Plan != "free";

    /// <summary>
    /// Whether billing features are enabled.
    /// </summary>
    public bool BillingEnabled { get; init; }

    /// <summary>
    /// Plan limits configuration for display.
    /// </summary>
    public PlanLimitsDto? PlanLimits { get; init; }
}
