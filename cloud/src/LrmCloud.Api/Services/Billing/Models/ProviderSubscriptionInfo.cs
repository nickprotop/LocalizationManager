namespace LrmCloud.Api.Services.Billing.Models;

/// <summary>
/// Provider-agnostic subscription information returned from payment providers.
/// </summary>
public sealed class ProviderSubscriptionInfo
{
    /// <summary>
    /// The subscription ID from the payment provider.
    /// </summary>
    public required string SubscriptionId { get; init; }

    /// <summary>
    /// The customer ID from the payment provider.
    /// </summary>
    public required string CustomerId { get; init; }

    /// <summary>
    /// The plan identifier (e.g., "team", "enterprise").
    /// </summary>
    public required string Plan { get; init; }

    /// <summary>
    /// Normalized subscription status.
    /// </summary>
    public required SubscriptionStatus Status { get; init; }

    /// <summary>
    /// When the current billing period ends.
    /// </summary>
    public DateTime? CurrentPeriodEnd { get; init; }

    /// <summary>
    /// Whether the subscription will cancel at the end of the current period.
    /// </summary>
    public bool CancelAtPeriodEnd { get; init; }

    /// <summary>
    /// When the subscription was created.
    /// </summary>
    public DateTime? CreatedAt { get; init; }

    /// <summary>
    /// When the subscription was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Normalized subscription status across payment providers.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// No subscription or unknown status.
    /// </summary>
    None,

    /// <summary>
    /// Subscription is active and in good standing.
    /// </summary>
    Active,

    /// <summary>
    /// Subscription is trialing (if trials are supported).
    /// </summary>
    Trialing,

    /// <summary>
    /// Payment is past due but subscription is not yet canceled.
    /// </summary>
    PastDue,

    /// <summary>
    /// Subscription has been canceled but is still active until period end.
    /// </summary>
    Canceled,

    /// <summary>
    /// Subscription is suspended/paused.
    /// </summary>
    Suspended,

    /// <summary>
    /// Subscription has expired and is no longer active.
    /// </summary>
    Expired,

    /// <summary>
    /// Subscription is incomplete (e.g., payment not yet confirmed).
    /// </summary>
    Incomplete
}
