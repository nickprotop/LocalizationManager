using LrmCloud.Api.Services.Billing.Models;
using LrmCloud.Shared.Configuration;
using Stripe;
using InvoiceStatus = LrmCloud.Api.Services.Billing.Models.InvoiceStatus;
using SubscriptionStatus = LrmCloud.Api.Services.Billing.Models.SubscriptionStatus;
using StripeConfig = Stripe.StripeConfiguration;
using LrmStripeConfiguration = LrmCloud.Shared.Configuration.StripeConfiguration;

namespace LrmCloud.Api.Services.Billing.Providers;

/// <summary>
/// Stripe payment provider implementation.
/// </summary>
public class StripePaymentProvider : IPaymentProvider
{
    private readonly CloudConfiguration _config;
    private readonly ILogger<StripePaymentProvider> _logger;

    public StripePaymentProvider(
        CloudConfiguration config,
        ILogger<StripePaymentProvider> logger)
    {
        _config = config;
        _logger = logger;

        // Configure Stripe API key
        if (IsEnabled)
        {
            StripeConfig.ApiKey = StripeConfig_?.SecretKey ?? "";
        }
    }

    /// <summary>
    /// Gets the Stripe configuration from the Payment section.
    /// </summary>
    private LrmStripeConfiguration? StripeConfig_ => _config.Payment?.Stripe;

    /// <inheritdoc />
    public string ProviderName => "stripe";

    /// <inheritdoc />
    public bool IsEnabled => StripeConfig_?.IsConfigured == true;

    /// <inheritdoc />
    public bool SupportsNativePortal => true;

    #region Customer & Subscription Management

    /// <inheritdoc />
    public async Task<string> GetOrCreateCustomerAsync(int userId, string email, string? displayName)
    {
        EnsureEnabled();

        // Search for existing customer by metadata
        var customerService = new CustomerService();
        var searchResult = await customerService.SearchAsync(new CustomerSearchOptions
        {
            Query = $"metadata['user_id']:'{userId}'"
        });

        if (searchResult.Data.Count > 0)
        {
            return searchResult.Data[0].Id;
        }

        // Create new Stripe customer
        var customer = await customerService.CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Name = displayName,
            Metadata = new Dictionary<string, string>
            {
                ["user_id"] = userId.ToString()
            }
        });

        _logger.LogInformation("Created Stripe customer {CustomerId} for user {UserId}", customer.Id, userId);

        return customer.Id;
    }

    /// <inheritdoc />
    public async Task<string> CreateCheckoutSessionAsync(string customerId, string plan, string successUrl, string cancelUrl)
    {
        EnsureEnabled();

        var priceId = GetPriceIdForPlan(plan);

        var sessionService = new Stripe.Checkout.SessionService();
        var session = await sessionService.CreateAsync(new Stripe.Checkout.SessionCreateOptions
        {
            Customer = customerId,
            Mode = "subscription",
            LineItems = new List<Stripe.Checkout.SessionLineItemOptions>
            {
                new()
                {
                    Price = priceId,
                    Quantity = 1
                }
            },
            SuccessUrl = successUrl + "?session_id={CHECKOUT_SESSION_ID}",
            CancelUrl = cancelUrl,
            SubscriptionData = new Stripe.Checkout.SessionSubscriptionDataOptions
            {
                Metadata = new Dictionary<string, string>
                {
                    ["plan"] = plan
                }
            },
            Metadata = new Dictionary<string, string>
            {
                ["plan"] = plan
            }
        });

        _logger.LogInformation("Created Stripe checkout session {SessionId} for customer {CustomerId}, plan {Plan}",
            session.Id, customerId, plan);

        return session.Url!;
    }

    /// <inheritdoc />
    public async Task<ProviderSubscriptionInfo?> GetSubscriptionAsync(string subscriptionId)
    {
        EnsureEnabled();

        try
        {
            var subscriptionService = new Stripe.SubscriptionService();
            var subscription = await subscriptionService.GetAsync(subscriptionId);

            return MapSubscription(subscription);
        }
        catch (StripeException ex) when (ex.StripeError?.Code == "resource_missing")
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task CancelSubscriptionAsync(string subscriptionId)
    {
        EnsureEnabled();

        var subscriptionService = new Stripe.SubscriptionService();
        await subscriptionService.UpdateAsync(subscriptionId, new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = true
        });

        _logger.LogInformation("Stripe subscription {SubscriptionId} marked for cancellation", subscriptionId);
    }

    /// <inheritdoc />
    public async Task ReactivateSubscriptionAsync(string subscriptionId)
    {
        EnsureEnabled();

        var subscriptionService = new Stripe.SubscriptionService();
        await subscriptionService.UpdateAsync(subscriptionId, new SubscriptionUpdateOptions
        {
            CancelAtPeriodEnd = false
        });

        _logger.LogInformation("Stripe subscription {SubscriptionId} reactivated", subscriptionId);
    }

    #endregion

    #region Custom Portal Support

    /// <inheritdoc />
    public async Task<List<InvoiceInfo>> GetInvoicesAsync(string customerId, int limit = 10)
    {
        EnsureEnabled();

        var invoiceService = new InvoiceService();
        var invoices = await invoiceService.ListAsync(new InvoiceListOptions
        {
            Customer = customerId,
            Limit = limit
        });

        return invoices.Data.Select(MapInvoice).ToList();
    }

    /// <inheritdoc />
    public async Task<PaymentMethodInfo?> GetPaymentMethodAsync(string customerId)
    {
        EnsureEnabled();

        var customerService = new CustomerService();
        var customer = await customerService.GetAsync(customerId, new CustomerGetOptions
        {
            Expand = new List<string> { "invoice_settings.default_payment_method" }
        });

        var defaultPaymentMethod = customer.InvoiceSettings?.DefaultPaymentMethod;
        if (defaultPaymentMethod == null)
        {
            return null;
        }

        return MapPaymentMethod(defaultPaymentMethod);
    }

    /// <inheritdoc />
    public async Task<string> GetUpdatePaymentMethodUrlAsync(string customerId, string returnUrl)
    {
        EnsureEnabled();

        // Use Stripe Customer Portal for updating payment method
        var portalService = new Stripe.BillingPortal.SessionService();
        var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl,
            FlowData = new Stripe.BillingPortal.SessionFlowDataOptions
            {
                Type = "payment_method_update"
            }
        });

        return session.Url;
    }

    #endregion

    #region Native Portal

    /// <inheritdoc />
    public async Task<string?> CreateNativePortalSessionAsync(string customerId, string returnUrl)
    {
        EnsureEnabled();

        var portalService = new Stripe.BillingPortal.SessionService();
        var session = await portalService.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
        {
            Customer = customerId,
            ReturnUrl = returnUrl
        });

        return session.Url;
    }

    #endregion

    #region Webhooks

    /// <inheritdoc />
    public Task<WebhookResult> ProcessWebhookAsync(string payload, string? signature)
    {
        EnsureEnabled();

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                payload,
                signature,
                StripeConfig_!.WebhookSecret
            );
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Stripe webhook signature verification failed");
            return Task.FromResult(WebhookResult.Failed(ProviderName, "Invalid signature"));
        }

        _logger.LogInformation("Processing Stripe webhook event: {EventType} ({EventId})",
            stripeEvent.Type, stripeEvent.Id);

        var result = stripeEvent.Type switch
        {
            "checkout.session.completed" => HandleCheckoutCompleted(stripeEvent),
            "customer.subscription.created" => HandleSubscriptionEvent(stripeEvent, WebhookEventType.SubscriptionCreated),
            "customer.subscription.updated" => HandleSubscriptionEvent(stripeEvent, WebhookEventType.SubscriptionUpdated),
            "customer.subscription.deleted" => HandleSubscriptionDeleted(stripeEvent),
            "invoice.payment_failed" => HandlePaymentFailed(stripeEvent),
            "invoice.paid" => HandleInvoicePaid(stripeEvent),
            _ => WebhookResult.Succeeded(ProviderName, WebhookEventType.Unknown)
        };

        return Task.FromResult(result);
    }

    private WebhookResult HandleCheckoutCompleted(Event stripeEvent)
    {
        var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
        if (session?.CustomerId == null)
        {
            return WebhookResult.Succeeded(ProviderName, WebhookEventType.CheckoutCompleted);
        }

        var plan = session.Metadata?.TryGetValue("plan", out var p) == true ? p : "team";

        return new WebhookResult
        {
            Success = true,
            ProviderName = ProviderName,
            EventType = WebhookEventType.CheckoutCompleted,
            CustomerId = session.CustomerId,
            SubscriptionId = session.SubscriptionId,
            Plan = plan,
            ProviderEventId = stripeEvent.Id
        };
    }

    private WebhookResult HandleSubscriptionEvent(Event stripeEvent, WebhookEventType eventType)
    {
        var subscription = stripeEvent.Data.Object as Stripe.Subscription;
        if (subscription?.CustomerId == null)
        {
            return WebhookResult.Succeeded(ProviderName, eventType);
        }

        var plan = GetPlanFromPriceId(subscription.Items.Data.FirstOrDefault()?.Price?.Id);

        return new WebhookResult
        {
            Success = true,
            ProviderName = ProviderName,
            EventType = eventType,
            CustomerId = subscription.CustomerId,
            SubscriptionId = subscription.Id,
            Plan = plan,
            NewStatus = MapStripeStatus(subscription.Status),
            CurrentPeriodEnd = subscription.CurrentPeriodEnd,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            ProviderEventId = stripeEvent.Id
        };
    }

    private WebhookResult HandleSubscriptionDeleted(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Stripe.Subscription;
        if (subscription?.CustomerId == null)
        {
            return WebhookResult.Succeeded(ProviderName, WebhookEventType.SubscriptionCanceled);
        }

        return new WebhookResult
        {
            Success = true,
            ProviderName = ProviderName,
            EventType = WebhookEventType.SubscriptionCanceled,
            CustomerId = subscription.CustomerId,
            SubscriptionId = subscription.Id,
            NewStatus = SubscriptionStatus.Canceled,
            ProviderEventId = stripeEvent.Id
        };
    }

    private WebhookResult HandlePaymentFailed(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice?.CustomerId == null)
        {
            return WebhookResult.Succeeded(ProviderName, WebhookEventType.PaymentFailed);
        }

        return new WebhookResult
        {
            Success = true,
            ProviderName = ProviderName,
            EventType = WebhookEventType.PaymentFailed,
            CustomerId = invoice.CustomerId,
            SubscriptionId = invoice.SubscriptionId,
            NewStatus = SubscriptionStatus.PastDue,
            ProviderEventId = stripeEvent.Id
        };
    }

    private WebhookResult HandleInvoicePaid(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice?.CustomerId == null)
        {
            return WebhookResult.Succeeded(ProviderName, WebhookEventType.PaymentSucceeded);
        }

        return new WebhookResult
        {
            Success = true,
            ProviderName = ProviderName,
            EventType = WebhookEventType.PaymentSucceeded,
            CustomerId = invoice.CustomerId,
            SubscriptionId = invoice.SubscriptionId,
            ProviderEventId = stripeEvent.Id
        };
    }

    #endregion

    #region Mapping Helpers

    private ProviderSubscriptionInfo MapSubscription(Stripe.Subscription subscription)
    {
        var plan = GetPlanFromPriceId(subscription.Items.Data.FirstOrDefault()?.Price?.Id);

        return new ProviderSubscriptionInfo
        {
            SubscriptionId = subscription.Id,
            CustomerId = subscription.CustomerId,
            Plan = plan,
            Status = MapStripeStatus(subscription.Status),
            CurrentPeriodEnd = subscription.CurrentPeriodEnd,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
            CreatedAt = subscription.Created,
            UpdatedAt = null // Stripe doesn't expose this directly
        };
    }

    private InvoiceInfo MapInvoice(Invoice invoice)
    {
        return new InvoiceInfo
        {
            InvoiceId = invoice.Id,
            InvoiceNumber = invoice.Number,
            Status = MapInvoiceStatus(invoice.Status),
            AmountTotal = invoice.Total,
            AmountPaid = invoice.AmountPaid,
            Currency = invoice.Currency,
            CreatedAt = invoice.Created,
            PaidAt = invoice.StatusTransitions?.PaidAt,
            DueDate = invoice.DueDate,
            PdfUrl = invoice.InvoicePdf,
            HostedUrl = invoice.HostedInvoiceUrl,
            Description = invoice.Description,
            PeriodStart = invoice.PeriodStart,
            PeriodEnd = invoice.PeriodEnd
        };
    }

    private PaymentMethodInfo MapPaymentMethod(PaymentMethod paymentMethod)
    {
        return new PaymentMethodInfo
        {
            PaymentMethodId = paymentMethod.Id,
            Type = paymentMethod.Type switch
            {
                "card" => PaymentMethodType.Card,
                "paypal" => PaymentMethodType.PayPal,
                "us_bank_account" or "sepa_debit" => PaymentMethodType.BankAccount,
                _ => PaymentMethodType.Unknown
            },
            CardBrand = paymentMethod.Card?.Brand,
            CardLast4 = paymentMethod.Card?.Last4,
            CardExpMonth = (int?)paymentMethod.Card?.ExpMonth,
            CardExpYear = (int?)paymentMethod.Card?.ExpYear,
            IsDefault = true // We're getting the default payment method
        };
    }

    private static SubscriptionStatus MapStripeStatus(string status)
    {
        return status switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trialing,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Canceled,
            "unpaid" => SubscriptionStatus.PastDue,
            "incomplete" => SubscriptionStatus.Incomplete,
            "incomplete_expired" => SubscriptionStatus.Expired,
            "paused" => SubscriptionStatus.Suspended,
            _ => SubscriptionStatus.None
        };
    }

    private static InvoiceStatus MapInvoiceStatus(string? status)
    {
        return status switch
        {
            "draft" => InvoiceStatus.Draft,
            "open" => InvoiceStatus.Open,
            "paid" => InvoiceStatus.Paid,
            "void" => InvoiceStatus.Void,
            "uncollectible" => InvoiceStatus.Uncollectible,
            _ => InvoiceStatus.Open
        };
    }

    #endregion

    #region Private Helpers

    private string GetPriceIdForPlan(string plan)
    {
        return plan.ToLowerInvariant() switch
        {
            "team" => StripeConfig_!.TeamPriceId,
            "enterprise" => StripeConfig_!.EnterprisePriceId,
            _ => throw new ArgumentException($"Invalid plan: {plan}")
        };
    }

    private string GetPlanFromPriceId(string? priceId)
    {
        if (string.IsNullOrEmpty(priceId)) return "free";

        if (priceId == StripeConfig_?.TeamPriceId) return "team";
        if (priceId == StripeConfig_?.EnterprisePriceId) return "enterprise";

        return "team"; // Default to team if unknown
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("Stripe payment provider is not enabled");
        }
    }

    #endregion
}
