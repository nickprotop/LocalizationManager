namespace LrmCloud.Api.Services.Billing.Models;

/// <summary>
/// Normalized webhook event types across payment providers.
/// </summary>
public enum WebhookEventType
{
    Unknown,
    CheckoutCompleted,
    SubscriptionCreated,
    SubscriptionActivated,
    SubscriptionUpdated,
    SubscriptionCanceled,
    SubscriptionSuspended,
    SubscriptionExpired,
    PaymentSucceeded,
    PaymentFailed
}
