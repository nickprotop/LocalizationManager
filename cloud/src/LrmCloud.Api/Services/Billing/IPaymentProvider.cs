using LrmCloud.Api.Services.Billing.Models;

namespace LrmCloud.Api.Services.Billing;

/// <summary>
/// Abstraction for payment providers (Stripe, PayPal, etc.).
/// Each provider implements this interface to handle provider-specific operations.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>
    /// The unique name of this payment provider (e.g., "stripe", "paypal").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Whether this provider is currently enabled and configured.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Whether this provider supports a native customer portal (like Stripe's Customer Portal).
    /// If false, use the custom portal methods instead.
    /// </summary>
    bool SupportsNativePortal { get; }

    #region Customer & Subscription Management

    /// <summary>
    /// Gets or creates a customer in the payment provider.
    /// </summary>
    /// <param name="userId">Internal user ID for reference.</param>
    /// <param name="email">Customer email address.</param>
    /// <param name="displayName">Customer display name (optional).</param>
    /// <returns>The payment provider's customer ID.</returns>
    Task<string> GetOrCreateCustomerAsync(int userId, string email, string? displayName);

    /// <summary>
    /// Creates a checkout session for subscribing to a plan.
    /// </summary>
    /// <param name="customerId">The payment provider's customer ID.</param>
    /// <param name="plan">Plan identifier ("team" or "enterprise").</param>
    /// <param name="successUrl">URL to redirect after successful checkout.</param>
    /// <param name="cancelUrl">URL to redirect if checkout is canceled.</param>
    /// <returns>URL to redirect the user to for checkout.</returns>
    Task<string> CreateCheckoutSessionAsync(string customerId, string plan, string successUrl, string cancelUrl);

    /// <summary>
    /// Gets subscription details from the payment provider.
    /// </summary>
    /// <param name="subscriptionId">The payment provider's subscription ID.</param>
    /// <returns>Subscription info, or null if not found.</returns>
    Task<ProviderSubscriptionInfo?> GetSubscriptionAsync(string subscriptionId);

    /// <summary>
    /// Cancels a subscription at the end of the current billing period.
    /// </summary>
    /// <param name="subscriptionId">The payment provider's subscription ID.</param>
    Task CancelSubscriptionAsync(string subscriptionId);

    /// <summary>
    /// Reactivates a subscription that was set to cancel at period end.
    /// </summary>
    /// <param name="subscriptionId">The payment provider's subscription ID.</param>
    Task ReactivateSubscriptionAsync(string subscriptionId);

    #endregion

    #region Custom Portal Support

    /// <summary>
    /// Gets recent invoices for a customer.
    /// Used by the custom billing portal.
    /// </summary>
    /// <param name="customerId">The payment provider's customer ID.</param>
    /// <param name="limit">Maximum number of invoices to return.</param>
    /// <returns>List of invoices.</returns>
    Task<List<InvoiceInfo>> GetInvoicesAsync(string customerId, int limit = 10);

    /// <summary>
    /// Gets the default payment method for a customer.
    /// Used by the custom billing portal.
    /// </summary>
    /// <param name="customerId">The payment provider's customer ID.</param>
    /// <returns>Payment method info, or null if none set.</returns>
    Task<PaymentMethodInfo?> GetPaymentMethodAsync(string customerId);

    /// <summary>
    /// Gets a URL where the customer can update their payment method.
    /// This may be a provider-hosted page or a checkout session for updating payment.
    /// </summary>
    /// <param name="customerId">The payment provider's customer ID.</param>
    /// <param name="returnUrl">URL to return to after updating.</param>
    /// <returns>URL to redirect the user to.</returns>
    Task<string> GetUpdatePaymentMethodUrlAsync(string customerId, string returnUrl);

    #endregion

    #region Native Portal (Optional)

    /// <summary>
    /// Creates a session for the provider's native customer portal (if supported).
    /// Returns null if the provider doesn't support native portals.
    /// </summary>
    /// <param name="customerId">The payment provider's customer ID.</param>
    /// <param name="returnUrl">URL to return to after portal session.</param>
    /// <returns>Portal URL, or null if not supported.</returns>
    Task<string?> CreateNativePortalSessionAsync(string customerId, string returnUrl);

    #endregion

    #region Webhooks

    /// <summary>
    /// Processes an incoming webhook from the payment provider.
    /// Validates the signature and parses the event.
    /// </summary>
    /// <param name="payload">Raw webhook payload body.</param>
    /// <param name="signature">Signature header value (provider-specific).</param>
    /// <returns>Parsed webhook result with normalized event data.</returns>
    Task<WebhookResult> ProcessWebhookAsync(string payload, string? signature);

    #endregion
}
