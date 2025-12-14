using LrmCloud.Api.Services.Billing.Models;
using LrmCloud.Shared.DTOs.Billing;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service interface for billing operations.
/// Acts as an orchestrator that delegates to payment providers.
/// </summary>
public interface IBillingService
{
    /// <summary>
    /// Check if any billing provider is enabled and configured.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Get the name of the active payment provider (e.g., "stripe", "paypal").
    /// </summary>
    string ActiveProviderName { get; }

    /// <summary>
    /// Get or create a payment customer for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>Payment provider customer ID</returns>
    Task<string> GetOrCreateCustomerAsync(int userId);

    /// <summary>
    /// Create a checkout session for subscription.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="plan">Plan name: "team" or "enterprise"</param>
    /// <param name="successUrl">URL to redirect after successful payment</param>
    /// <param name="cancelUrl">URL to redirect if user cancels</param>
    /// <returns>Checkout session URL</returns>
    Task<string> CreateCheckoutSessionAsync(int userId, string plan, string successUrl, string cancelUrl);

    /// <summary>
    /// Create a portal session for subscription management.
    /// Returns the provider's native portal URL if supported, otherwise returns null.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="returnUrl">URL to return to after portal session</param>
    /// <returns>Portal session URL, or null if not supported</returns>
    Task<string?> CreatePortalSessionAsync(int userId, string returnUrl);

    /// <summary>
    /// Get the current subscription status for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>Subscription details or null if no subscription</returns>
    Task<SubscriptionDto?> GetSubscriptionAsync(int userId);

    /// <summary>
    /// Cancel a user's subscription at period end.
    /// </summary>
    /// <param name="userId">The user ID</param>
    Task CancelSubscriptionAsync(int userId);

    /// <summary>
    /// Reactivate a canceled subscription before period end.
    /// </summary>
    /// <param name="userId">The user ID</param>
    Task ReactivateSubscriptionAsync(int userId);

    /// <summary>
    /// Process a webhook result from a payment provider.
    /// Updates user entities based on the webhook event.
    /// </summary>
    /// <param name="result">Parsed webhook result from the provider</param>
    Task HandleWebhookResultAsync(WebhookResult result);

    /// <summary>
    /// Get recent invoices for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="limit">Maximum number of invoices to return</param>
    /// <returns>List of invoices</returns>
    Task<List<InvoiceInfo>> GetInvoicesAsync(int userId, int limit = 10);

    /// <summary>
    /// Get the default payment method for a user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <returns>Payment method info, or null if none set</returns>
    Task<PaymentMethodInfo?> GetPaymentMethodAsync(int userId);

    /// <summary>
    /// Get a URL where the user can update their payment method.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="returnUrl">URL to return to after updating</param>
    /// <returns>URL to redirect the user to</returns>
    Task<string> GetUpdatePaymentMethodUrlAsync(int userId, string returnUrl);
}
