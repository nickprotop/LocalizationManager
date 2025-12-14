using LrmCloud.Api.Data;
using LrmCloud.Api.Services.Billing;
using LrmCloud.Api.Services.Billing.Models;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Billing;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

/// <summary>
/// Billing service that orchestrates payment operations across providers.
/// Handles entity updates while delegating provider-specific operations to IPaymentProvider.
/// </summary>
public class BillingService : IBillingService
{
    private readonly AppDbContext _db;
    private readonly CloudConfiguration _config;
    private readonly PaymentProviderFactory _providerFactory;
    private readonly ILogger<BillingService> _logger;

    public BillingService(
        AppDbContext db,
        CloudConfiguration config,
        PaymentProviderFactory providerFactory,
        ILogger<BillingService> logger)
    {
        _db = db;
        _config = config;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsEnabled => _providerFactory.HasEnabledProvider();

    /// <inheritdoc />
    public string ActiveProviderName => _providerFactory.ActiveProviderName;

    /// <inheritdoc />
    public async Task<string> GetOrCreateCustomerAsync(int userId)
    {
        EnsureEnabled();

        var user = await GetUserAsync(userId);
        var provider = _providerFactory.GetActiveProvider();

        // Check if user already has a customer ID for this provider
        if (!string.IsNullOrEmpty(user.PaymentCustomerId) && user.PaymentProvider == provider.ProviderName)
        {
            return user.PaymentCustomerId;
        }

        // Create new customer with provider
        var customerId = await provider.GetOrCreateCustomerAsync(userId, user.Email, user.DisplayName ?? user.Username);

        // Save customer ID
        user.PaymentCustomerId = customerId;
        user.PaymentProvider = provider.ProviderName;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created {Provider} customer {CustomerId} for user {UserId}",
            provider.ProviderName, customerId, userId);

        return customerId;
    }

    /// <inheritdoc />
    public async Task<string> CreateCheckoutSessionAsync(int userId, string plan, string successUrl, string cancelUrl)
    {
        EnsureEnabled();

        var customerId = await GetOrCreateCustomerAsync(userId);
        var provider = _providerFactory.GetActiveProvider();

        var checkoutUrl = await provider.CreateCheckoutSessionAsync(customerId, plan, successUrl, cancelUrl);

        _logger.LogInformation("Created checkout session for user {UserId}, plan {Plan}, provider {Provider}",
            userId, plan, provider.ProviderName);

        return checkoutUrl;
    }

    /// <inheritdoc />
    public async Task<string?> CreatePortalSessionAsync(int userId, string returnUrl)
    {
        EnsureEnabled();

        var user = await GetUserAsync(userId);

        if (string.IsNullOrEmpty(user.PaymentCustomerId))
        {
            throw new InvalidOperationException("User has no payment customer ID");
        }

        // Get the provider that the user is using
        var provider = GetProviderForUser(user);

        if (!provider.SupportsNativePortal)
        {
            return null;
        }

        return await provider.CreateNativePortalSessionAsync(user.PaymentCustomerId, returnUrl);
    }

    /// <inheritdoc />
    public async Task<SubscriptionDto?> GetSubscriptionAsync(int userId)
    {
        var user = await GetUserAsync(userId);

        return new SubscriptionDto
        {
            Plan = user.Plan,
            Status = user.SubscriptionStatus,
            CurrentPeriodEnd = user.SubscriptionCurrentPeriodEnd,
            CancelAtPeriodEnd = user.CancelAtPeriodEnd,
            PaymentSubscriptionId = user.PaymentSubscriptionId,
            PaymentProvider = user.PaymentProvider,
            BillingEnabled = IsEnabled,
            PlanLimits = GetPlanLimits()
        };
    }

    /// <summary>
    /// Gets the plan limits configuration from config.
    /// </summary>
    private PlanLimitsDto GetPlanLimits()
    {
        var limits = _config.Limits;
        return new PlanLimitsDto
        {
            Free = new PlanTierLimits
            {
                TranslationChars = limits.FreeTranslationChars,
                OtherChars = limits.FreeOtherChars,
                MaxProjects = limits.FreeMaxProjects,
                MaxTeamMembers = 0,
                MaxApiKeys = limits.FreeMaxApiKeys,
                Price = 0
            },
            Team = new PlanTierLimits
            {
                TranslationChars = limits.TeamTranslationChars,
                OtherChars = limits.TeamOtherChars,
                MaxProjects = int.MaxValue,
                MaxTeamMembers = limits.TeamMaxMembers,
                MaxApiKeys = limits.TeamMaxApiKeys,
                Price = 9
            },
            Enterprise = new PlanTierLimits
            {
                TranslationChars = limits.EnterpriseTranslationChars,
                OtherChars = limits.EnterpriseOtherChars,
                MaxProjects = int.MaxValue,
                MaxTeamMembers = int.MaxValue,
                MaxApiKeys = int.MaxValue,
                Price = 29
            }
        };
    }

    /// <inheritdoc />
    public async Task CancelSubscriptionAsync(int userId)
    {
        EnsureEnabled();

        var user = await GetUserAsync(userId);

        if (string.IsNullOrEmpty(user.PaymentSubscriptionId))
        {
            throw new InvalidOperationException("User has no active subscription");
        }

        var provider = GetProviderForUser(user);
        await provider.CancelSubscriptionAsync(user.PaymentSubscriptionId);

        user.CancelAtPeriodEnd = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Subscription {SubscriptionId} marked for cancellation for user {UserId}",
            user.PaymentSubscriptionId, userId);
    }

    /// <inheritdoc />
    public async Task ReactivateSubscriptionAsync(int userId)
    {
        EnsureEnabled();

        var user = await GetUserAsync(userId);

        if (string.IsNullOrEmpty(user.PaymentSubscriptionId))
        {
            throw new InvalidOperationException("User has no subscription to reactivate");
        }

        var provider = GetProviderForUser(user);
        await provider.ReactivateSubscriptionAsync(user.PaymentSubscriptionId);

        user.CancelAtPeriodEnd = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Subscription {SubscriptionId} reactivated for user {UserId}",
            user.PaymentSubscriptionId, userId);
    }

    /// <inheritdoc />
    public async Task HandleWebhookResultAsync(WebhookResult result)
    {
        if (!result.Success)
        {
            _logger.LogWarning("Webhook processing failed: {Error}", result.ErrorMessage);
            return;
        }

        if (string.IsNullOrEmpty(result.CustomerId) && string.IsNullOrEmpty(result.SubscriptionId))
        {
            _logger.LogDebug("Webhook event {EventType} has no customer or subscription ID, skipping", result.EventType);
            return;
        }

        // Find user by payment customer ID or subscription ID
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.PaymentProvider == result.ProviderName &&
            (u.PaymentCustomerId == result.CustomerId || u.PaymentSubscriptionId == result.SubscriptionId));

        if (user == null)
        {
            _logger.LogWarning("No user found for {Provider} customer {CustomerId} / subscription {SubscriptionId}",
                result.ProviderName, result.CustomerId, result.SubscriptionId);
            return;
        }

        switch (result.EventType)
        {
            case WebhookEventType.CheckoutCompleted:
                await HandleCheckoutCompleted(user, result);
                break;

            case WebhookEventType.SubscriptionCreated:
            case WebhookEventType.SubscriptionActivated:
            case WebhookEventType.SubscriptionUpdated:
                await HandleSubscriptionUpdated(user, result);
                break;

            case WebhookEventType.SubscriptionCanceled:
            case WebhookEventType.SubscriptionExpired:
                await HandleSubscriptionCanceled(user, result);
                break;

            case WebhookEventType.SubscriptionSuspended:
            case WebhookEventType.PaymentFailed:
                await HandlePaymentFailed(user, result);
                break;

            case WebhookEventType.PaymentSucceeded:
                await HandlePaymentSucceeded(user, result);
                break;

            default:
                _logger.LogDebug("Unhandled webhook event type: {EventType}", result.EventType);
                break;
        }
    }

    /// <inheritdoc />
    public async Task<List<InvoiceInfo>> GetInvoicesAsync(int userId, int limit = 10)
    {
        EnsureEnabled();

        var user = await GetUserAsync(userId);

        if (string.IsNullOrEmpty(user.PaymentCustomerId))
        {
            return new List<InvoiceInfo>();
        }

        var provider = GetProviderForUser(user);
        // PayPal uses subscription ID for invoices/transactions, Stripe uses customer ID
        var identifier = GetProviderIdentifier(user, provider);
        return await provider.GetInvoicesAsync(identifier, limit);
    }

    /// <inheritdoc />
    public async Task<PaymentMethodInfo?> GetPaymentMethodAsync(int userId)
    {
        EnsureEnabled();

        var user = await GetUserAsync(userId);

        if (string.IsNullOrEmpty(user.PaymentCustomerId))
        {
            return null;
        }

        var provider = GetProviderForUser(user);
        // PayPal uses subscription ID for payment method lookup, Stripe uses customer ID
        var identifier = GetProviderIdentifier(user, provider);
        return await provider.GetPaymentMethodAsync(identifier);
    }

    /// <inheritdoc />
    public async Task<string> GetUpdatePaymentMethodUrlAsync(int userId, string returnUrl)
    {
        EnsureEnabled();

        var user = await GetUserAsync(userId);

        if (string.IsNullOrEmpty(user.PaymentCustomerId))
        {
            throw new InvalidOperationException("User has no payment customer ID");
        }

        var provider = GetProviderForUser(user);
        return await provider.GetUpdatePaymentMethodUrlAsync(user.PaymentCustomerId, returnUrl);
    }

    // ==========================================================================
    // Webhook Event Handlers
    // ==========================================================================

    private async Task HandleCheckoutCompleted(User user, WebhookResult result)
    {
        if (string.IsNullOrEmpty(result.SubscriptionId))
        {
            return;
        }

        var plan = result.Plan ?? "team";

        user.Plan = plan;
        user.PaymentSubscriptionId = result.SubscriptionId;
        user.SubscriptionStatus = "active";
        user.CancelAtPeriodEnd = result.CancelAtPeriodEnd ?? false;

        if (result.CurrentPeriodEnd.HasValue)
        {
            user.SubscriptionCurrentPeriodEnd = result.CurrentPeriodEnd.Value;
        }

        // Update limits based on plan
        user.TranslationCharsLimit = _config.Limits.GetTranslationCharsLimit(plan);
        user.OtherCharsLimit = _config.Limits.GetOtherCharsLimit(plan);

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Checkout completed for user {UserId}, plan {Plan}", user.Id, plan);
    }

    private async Task HandleSubscriptionUpdated(User user, WebhookResult result)
    {
        var plan = result.Plan ?? user.Plan;
        var status = MapSubscriptionStatus(result.NewStatus);

        user.Plan = plan;

        if (!string.IsNullOrEmpty(result.SubscriptionId))
        {
            user.PaymentSubscriptionId = result.SubscriptionId;
        }

        user.SubscriptionStatus = status;

        if (result.CurrentPeriodEnd.HasValue)
        {
            user.SubscriptionCurrentPeriodEnd = result.CurrentPeriodEnd.Value;
        }

        if (result.CancelAtPeriodEnd.HasValue)
        {
            user.CancelAtPeriodEnd = result.CancelAtPeriodEnd.Value;
        }

        // Update limits based on plan
        user.TranslationCharsLimit = _config.Limits.GetTranslationCharsLimit(plan);
        user.OtherCharsLimit = _config.Limits.GetOtherCharsLimit(plan);

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Subscription updated for user {UserId}, status {Status}, plan {Plan}",
            user.Id, status, plan);
    }

    private async Task HandleSubscriptionCanceled(User user, WebhookResult result)
    {
        // Downgrade to free
        user.Plan = "free";
        user.PaymentSubscriptionId = null;
        user.SubscriptionStatus = "canceled";
        user.SubscriptionCurrentPeriodEnd = null;
        user.CancelAtPeriodEnd = false;

        // Update limits to free tier
        user.TranslationCharsLimit = _config.Limits.FreeTranslationChars;
        user.OtherCharsLimit = _config.Limits.FreeOtherChars;

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Subscription canceled for user {UserId}, downgraded to free", user.Id);
    }

    private async Task HandlePaymentFailed(User user, WebhookResult result)
    {
        user.SubscriptionStatus = "past_due";
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogWarning("Payment failed for user {UserId}", user.Id);
    }

    private async Task HandlePaymentSucceeded(User user, WebhookResult result)
    {
        // Ensure subscription is marked as active
        if (user.SubscriptionStatus == "past_due")
        {
            user.SubscriptionStatus = "active";
            user.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Payment received, subscription reactivated for user {UserId}", user.Id);
        }
    }

    // ==========================================================================
    // Helper Methods
    // ==========================================================================

    private async Task<User> GetUserAsync(int userId)
    {
        var user = await _db.Users.FindAsync(userId);

        if (user == null || user.DeletedAt != null)
        {
            throw new InvalidOperationException($"User not found: {userId}");
        }

        return user;
    }

    private IPaymentProvider GetProviderForUser(User user)
    {
        // If user has a specific provider, use that
        if (!string.IsNullOrEmpty(user.PaymentProvider) &&
            _providerFactory.TryGetProvider(user.PaymentProvider, out var provider) &&
            provider != null)
        {
            return provider;
        }

        // Otherwise use the active provider
        return _providerFactory.GetActiveProvider();
    }

    /// <summary>
    /// Gets the appropriate identifier for provider-specific operations.
    /// Stripe uses customer ID for invoice/payment lookups.
    /// PayPal uses subscription ID since it doesn't have a separate customer concept.
    /// </summary>
    private static string GetProviderIdentifier(User user, IPaymentProvider provider)
    {
        // PayPal needs subscription ID for invoices and payment method lookups
        if (provider.ProviderName == "paypal" && !string.IsNullOrEmpty(user.PaymentSubscriptionId))
        {
            return user.PaymentSubscriptionId;
        }

        // Stripe and others use customer ID
        return user.PaymentCustomerId ?? "";
    }

    private static string MapSubscriptionStatus(SubscriptionStatus? status)
    {
        return status switch
        {
            SubscriptionStatus.Active => "active",
            SubscriptionStatus.Trialing => "trialing",
            SubscriptionStatus.PastDue => "past_due",
            SubscriptionStatus.Canceled => "canceled",
            SubscriptionStatus.Suspended => "past_due",
            SubscriptionStatus.Expired => "canceled",
            SubscriptionStatus.Incomplete => "incomplete",
            _ => "none"
        };
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Billing is not enabled");
        }
    }
}
