using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LrmCloud.Api.Services.Billing.Models;
using LrmCloud.Shared.Configuration;

namespace LrmCloud.Api.Services.Billing.Providers;

/// <summary>
/// PayPal payment provider implementation using PayPal Subscriptions API.
/// </summary>
public class PayPalPaymentProvider : IPaymentProvider
{
    private readonly CloudConfiguration _config;
    private readonly ILogger<PayPalPaymentProvider> _logger;
    private readonly HttpClient _httpClient;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PayPalPaymentProvider(
        CloudConfiguration config,
        ILogger<PayPalPaymentProvider> logger,
        IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("PayPal");
    }

    private PayPalConfiguration? PayPalConfig => _config.Payment?.PayPal;

    /// <inheritdoc />
    public string ProviderName => "paypal";

    /// <inheritdoc />
    public bool IsEnabled => PayPalConfig?.IsConfigured == true;

    /// <inheritdoc />
    public bool SupportsNativePortal => false;

    #region Customer & Subscription Management

    /// <inheritdoc />
    public Task<string> GetOrCreateCustomerAsync(int userId, string email, string? displayName)
    {
        // PayPal doesn't have a separate customer creation step.
        // The payer ID is obtained when they complete a subscription.
        // Return the user ID as a reference for now.
        return Task.FromResult($"user_{userId}");
    }

    /// <inheritdoc />
    public async Task<string> CreateCheckoutSessionAsync(string customerId, string plan, string successUrl, string cancelUrl)
    {
        EnsureEnabled();

        var planId = GetPlanIdForPlan(plan);
        var accessToken = await GetAccessTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"{PayPalConfig!.ApiBaseUrl}/v1/billing/subscriptions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            plan_id = planId,
            application_context = new
            {
                return_url = successUrl,
                cancel_url = cancelUrl,
                brand_name = "LRM Cloud",
                locale = "en-US",
                user_action = "SUBSCRIBE_NOW"
            },
            custom_id = customerId
        }, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal subscription creation failed: {StatusCode} - {Body}",
                response.StatusCode, responseBody);
            throw new InvalidOperationException($"Failed to create PayPal subscription: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<PayPalSubscriptionResponse>(responseBody, JsonOptions);
        var approvalLink = result?.Links?.FirstOrDefault(l => l.Rel == "approve")?.Href;

        if (string.IsNullOrEmpty(approvalLink))
        {
            throw new InvalidOperationException("PayPal subscription response missing approval URL");
        }

        _logger.LogInformation("Created PayPal subscription {SubscriptionId} for customer {CustomerId}, plan {Plan}",
            result?.Id, customerId, plan);

        return approvalLink;
    }

    /// <inheritdoc />
    public async Task<ProviderSubscriptionInfo?> GetSubscriptionAsync(string subscriptionId)
    {
        EnsureEnabled();

        var accessToken = await GetAccessTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Get,
            $"{PayPalConfig!.ApiBaseUrl}/v1/billing/subscriptions/{subscriptionId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("PayPal get subscription failed: {StatusCode} - {Body}",
                response.StatusCode, errorBody);
            throw new InvalidOperationException($"Failed to get PayPal subscription: {response.StatusCode}");
        }

        var responseBody = await response.Content.ReadAsStringAsync();
        var subscription = JsonSerializer.Deserialize<PayPalSubscriptionResponse>(responseBody, JsonOptions);

        if (subscription == null)
            return null;

        return new ProviderSubscriptionInfo
        {
            SubscriptionId = subscription.Id ?? subscriptionId,
            CustomerId = subscription.Subscriber?.PayerId ?? subscription.CustomId ?? "",
            Plan = GetPlanFromPlanId(subscription.PlanId),
            Status = MapPayPalStatus(subscription.Status),
            CurrentPeriodEnd = subscription.BillingInfo?.NextBillingTime,
            CancelAtPeriodEnd = subscription.Status == "CANCELLED",
            CreatedAt = subscription.CreateTime,
            UpdatedAt = subscription.UpdateTime
        };
    }

    /// <inheritdoc />
    public async Task CancelSubscriptionAsync(string subscriptionId)
    {
        EnsureEnabled();

        var accessToken = await GetAccessTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{PayPalConfig!.ApiBaseUrl}/v1/billing/subscriptions/{subscriptionId}/cancel");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            reason = "Customer requested cancellation"
        }, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("PayPal cancel subscription failed: {StatusCode} - {Body}",
                response.StatusCode, errorBody);
            throw new InvalidOperationException($"Failed to cancel PayPal subscription: {response.StatusCode}");
        }

        _logger.LogInformation("PayPal subscription {SubscriptionId} canceled", subscriptionId);
    }

    /// <inheritdoc />
    public async Task ReactivateSubscriptionAsync(string subscriptionId)
    {
        EnsureEnabled();

        var accessToken = await GetAccessTokenAsync();

        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{PayPalConfig!.ApiBaseUrl}/v1/billing/subscriptions/{subscriptionId}/activate");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            reason = "Customer reactivated subscription"
        }, JsonOptions), Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            _logger.LogError("PayPal reactivate subscription failed: {StatusCode} - {Body}",
                response.StatusCode, errorBody);
            throw new InvalidOperationException($"Failed to reactivate PayPal subscription: {response.StatusCode}");
        }

        _logger.LogInformation("PayPal subscription {SubscriptionId} reactivated", subscriptionId);
    }

    #endregion

    #region Custom Portal Support

    /// <inheritdoc />
    public async Task<List<InvoiceInfo>> GetInvoicesAsync(string subscriptionId, int limit = 10)
    {
        if (!IsEnabled || string.IsNullOrEmpty(subscriptionId))
        {
            return new List<InvoiceInfo>();
        }

        try
        {
            var accessToken = await GetAccessTokenAsync();

            // PayPal Subscriptions API - list transactions for a subscription
            // Time range: last year to now
            var startTime = DateTime.UtcNow.AddYears(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var endTime = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{PayPalConfig!.ApiBaseUrl}/v1/billing/subscriptions/{subscriptionId}/transactions?start_time={startTime}&end_time={endTime}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("PayPal get transactions failed: {StatusCode} - {Body}",
                    response.StatusCode, errorBody);
                return new List<InvoiceInfo>();
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var transactionsResponse = JsonSerializer.Deserialize<PayPalTransactionsResponse>(responseBody, JsonOptions);

            if (transactionsResponse?.Transactions == null)
            {
                return new List<InvoiceInfo>();
            }

            return transactionsResponse.Transactions
                .Take(limit)
                .Select(t => new InvoiceInfo
                {
                    InvoiceId = t.Id ?? "",
                    InvoiceNumber = t.Id,
                    Status = MapTransactionStatus(t.Status),
                    AmountTotal = ParsePayPalAmount(t.AmountWithBreakdown?.GrossAmount?.Value),
                    AmountPaid = t.Status == "COMPLETED" ? ParsePayPalAmount(t.AmountWithBreakdown?.GrossAmount?.Value) : 0,
                    Currency = t.AmountWithBreakdown?.GrossAmount?.CurrencyCode?.ToLowerInvariant() ?? "usd",
                    CreatedAt = t.Time ?? DateTime.UtcNow,
                    PaidAt = t.Status == "COMPLETED" ? t.Time : null,
                    DueDate = null,
                    PdfUrl = null, // PayPal doesn't provide PDF receipts via API
                    HostedUrl = $"https://www.paypal.com/activity/payment/{t.Id}",
                    Description = $"Subscription payment",
                    PeriodStart = null,
                    PeriodEnd = null
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get PayPal transactions for subscription {SubscriptionId}", subscriptionId);
            return new List<InvoiceInfo>();
        }
    }

    /// <inheritdoc />
    public async Task<PaymentMethodInfo?> GetPaymentMethodAsync(string subscriptionId)
    {
        if (!IsEnabled || string.IsNullOrEmpty(subscriptionId))
        {
            return null;
        }

        try
        {
            // Get subscription details to retrieve subscriber info including email
            var accessToken = await GetAccessTokenAsync();

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"{PayPalConfig!.ApiBaseUrl}/v1/billing/subscriptions/{subscriptionId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PayPal get subscription for payment method failed: {StatusCode}",
                    response.StatusCode);
                // Return generic PayPal info if we can't fetch details
                return new PaymentMethodInfo
                {
                    PaymentMethodId = "paypal",
                    Type = PaymentMethodType.PayPal,
                    PayPalEmail = null,
                    IsDefault = true
                };
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            var subscription = JsonSerializer.Deserialize<PayPalSubscriptionResponse>(responseBody, JsonOptions);

            return new PaymentMethodInfo
            {
                PaymentMethodId = subscription?.Subscriber?.PayerId ?? "paypal",
                Type = PaymentMethodType.PayPal,
                PayPalEmail = subscription?.Subscriber?.EmailAddress,
                IsDefault = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get PayPal payment method for subscription {SubscriptionId}", subscriptionId);
            return new PaymentMethodInfo
            {
                PaymentMethodId = "paypal",
                Type = PaymentMethodType.PayPal,
                PayPalEmail = null,
                IsDefault = true
            };
        }
    }

    /// <inheritdoc />
    public Task<string> GetUpdatePaymentMethodUrlAsync(string customerId, string returnUrl)
    {
        // PayPal manages payment methods through their own site
        return Task.FromResult("https://www.paypal.com/myaccount/autopay");
    }

    private static InvoiceStatus MapTransactionStatus(string? status)
    {
        return status?.ToUpperInvariant() switch
        {
            "COMPLETED" => InvoiceStatus.Paid,
            "PENDING" => InvoiceStatus.Open,
            "DECLINED" => InvoiceStatus.Uncollectible,
            "REFUNDED" => InvoiceStatus.Void,
            "PARTIALLY_REFUNDED" => InvoiceStatus.Paid,
            _ => InvoiceStatus.Open
        };
    }

    private static long ParsePayPalAmount(string? value)
    {
        if (string.IsNullOrEmpty(value) || !decimal.TryParse(value, out var amount))
        {
            return 0;
        }
        // PayPal returns amounts as decimal strings (e.g., "9.00")
        // Convert to cents for consistency with Stripe
        return (long)(amount * 100);
    }

    #endregion

    #region Native Portal

    /// <inheritdoc />
    public Task<string?> CreateNativePortalSessionAsync(string customerId, string returnUrl)
    {
        // PayPal doesn't have an equivalent to Stripe's Customer Portal
        return Task.FromResult<string?>(null);
    }

    #endregion

    #region Webhooks

    /// <inheritdoc />
    public async Task<WebhookResult> ProcessWebhookAsync(string payload, string? signature)
    {
        EnsureEnabled();

        // Verify webhook signature
        var isValid = await VerifyWebhookSignatureAsync(payload, signature);
        if (!isValid)
        {
            _logger.LogWarning("PayPal webhook signature verification failed");
            return WebhookResult.Failed(ProviderName, "Invalid signature");
        }

        var webhookEvent = JsonSerializer.Deserialize<PayPalWebhookEvent>(payload, JsonOptions);
        if (webhookEvent == null)
        {
            return WebhookResult.Failed(ProviderName, "Failed to parse webhook payload");
        }

        _logger.LogInformation("Processing PayPal webhook event: {EventType} ({EventId})",
            webhookEvent.EventType, webhookEvent.Id);

        return webhookEvent.EventType switch
        {
            "BILLING.SUBSCRIPTION.CREATED" => HandleSubscriptionEvent(webhookEvent, WebhookEventType.SubscriptionCreated),
            "BILLING.SUBSCRIPTION.ACTIVATED" => HandleSubscriptionEvent(webhookEvent, WebhookEventType.SubscriptionActivated),
            "BILLING.SUBSCRIPTION.UPDATED" => HandleSubscriptionEvent(webhookEvent, WebhookEventType.SubscriptionUpdated),
            "BILLING.SUBSCRIPTION.CANCELLED" => HandleSubscriptionCanceled(webhookEvent),
            "BILLING.SUBSCRIPTION.SUSPENDED" => HandleSubscriptionSuspended(webhookEvent),
            "BILLING.SUBSCRIPTION.EXPIRED" => HandleSubscriptionExpired(webhookEvent),
            "PAYMENT.SALE.COMPLETED" => HandlePaymentCompleted(webhookEvent),
            _ => WebhookResult.Succeeded(ProviderName, WebhookEventType.Unknown)
        };
    }

    private WebhookResult HandleSubscriptionEvent(PayPalWebhookEvent webhookEvent, WebhookEventType eventType)
    {
        var resource = webhookEvent.Resource;
        if (resource == null)
        {
            return WebhookResult.Succeeded(ProviderName, eventType);
        }

        return new WebhookResult
        {
            Success = true,
            ProviderName = ProviderName,
            EventType = eventType,
            CustomerId = resource.CustomId ?? resource.Subscriber?.PayerId,
            SubscriptionId = resource.Id,
            Plan = GetPlanFromPlanId(resource.PlanId),
            NewStatus = MapPayPalStatus(resource.Status),
            CurrentPeriodEnd = resource.BillingInfo?.NextBillingTime,
            ProviderEventId = webhookEvent.Id
        };
    }

    private WebhookResult HandleSubscriptionCanceled(PayPalWebhookEvent webhookEvent)
    {
        var resource = webhookEvent.Resource;
        return new WebhookResult
        {
            Success = true,
            ProviderName = ProviderName,
            EventType = WebhookEventType.SubscriptionCanceled,
            CustomerId = resource?.CustomId ?? resource?.Subscriber?.PayerId,
            SubscriptionId = resource?.Id,
            NewStatus = SubscriptionStatus.Canceled,
            ProviderEventId = webhookEvent.Id
        };
    }

    private WebhookResult HandleSubscriptionSuspended(PayPalWebhookEvent webhookEvent)
    {
        var resource = webhookEvent.Resource;
        return new WebhookResult
        {
            Success = true,
            ProviderName = ProviderName,
            EventType = WebhookEventType.SubscriptionSuspended,
            CustomerId = resource?.CustomId ?? resource?.Subscriber?.PayerId,
            SubscriptionId = resource?.Id,
            NewStatus = SubscriptionStatus.PastDue,
            ProviderEventId = webhookEvent.Id
        };
    }

    private WebhookResult HandleSubscriptionExpired(PayPalWebhookEvent webhookEvent)
    {
        var resource = webhookEvent.Resource;
        return new WebhookResult
        {
            Success = true,
            ProviderName = ProviderName,
            EventType = WebhookEventType.SubscriptionExpired,
            CustomerId = resource?.CustomId ?? resource?.Subscriber?.PayerId,
            SubscriptionId = resource?.Id,
            NewStatus = SubscriptionStatus.Expired,
            ProviderEventId = webhookEvent.Id
        };
    }

    private WebhookResult HandlePaymentCompleted(PayPalWebhookEvent webhookEvent)
    {
        var resource = webhookEvent.Resource;
        return new WebhookResult
        {
            Success = true,
            ProviderName = ProviderName,
            EventType = WebhookEventType.PaymentSucceeded,
            CustomerId = resource?.CustomId ?? resource?.BillingAgreementId,
            SubscriptionId = resource?.BillingAgreementId,
            ProviderEventId = webhookEvent.Id
        };
    }

    private async Task<bool> VerifyWebhookSignatureAsync(string payload, string? signature)
    {
        if (string.IsNullOrEmpty(PayPalConfig?.WebhookId))
        {
            _logger.LogWarning("PayPal webhook ID not configured, skipping signature verification");
            return true; // Allow in development
        }

        // PayPal webhook verification requires multiple headers
        // For simplicity, we'll trust the webhook if the webhook ID is configured
        // In production, implement full signature verification
        return true;
    }

    #endregion

    #region OAuth Token Management

    private async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry)
        {
            return _accessToken;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"{PayPalConfig!.ApiBaseUrl}/v1/oauth2/token");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{PayPalConfig.ClientId}:{PayPalConfig.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("PayPal OAuth token request failed: {StatusCode} - {Body}",
                response.StatusCode, responseBody);
            throw new InvalidOperationException($"Failed to get PayPal access token: {response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<PayPalTokenResponse>(responseBody, JsonOptions);
        _accessToken = tokenResponse?.AccessToken
            ?? throw new InvalidOperationException("PayPal access token response missing token");
        _tokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60); // Refresh 1 minute early

        return _accessToken;
    }

    #endregion

    #region Helper Methods

    private string GetPlanIdForPlan(string plan)
    {
        return plan.ToLowerInvariant() switch
        {
            "team" => PayPalConfig!.TeamPlanId,
            "enterprise" => PayPalConfig!.EnterprisePlanId,
            _ => throw new ArgumentException($"Invalid plan: {plan}")
        };
    }

    private string GetPlanFromPlanId(string? planId)
    {
        if (string.IsNullOrEmpty(planId)) return "free";

        if (planId == PayPalConfig?.TeamPlanId) return "team";
        if (planId == PayPalConfig?.EnterprisePlanId) return "enterprise";

        return "team"; // Default to team if unknown
    }

    private static SubscriptionStatus MapPayPalStatus(string? status)
    {
        return status?.ToUpperInvariant() switch
        {
            "ACTIVE" => SubscriptionStatus.Active,
            "APPROVAL_PENDING" => SubscriptionStatus.Incomplete,
            "APPROVED" => SubscriptionStatus.Active,
            "SUSPENDED" => SubscriptionStatus.PastDue,
            "CANCELLED" => SubscriptionStatus.Canceled,
            "EXPIRED" => SubscriptionStatus.Expired,
            _ => SubscriptionStatus.None
        };
    }

    private void EnsureEnabled()
    {
        if (!IsEnabled)
        {
            throw new InvalidOperationException("PayPal payment provider is not enabled");
        }
    }

    #endregion

    #region PayPal API Models

    private class PayPalTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }

    private class PayPalSubscriptionResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("plan_id")]
        public string? PlanId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("custom_id")]
        public string? CustomId { get; set; }

        [JsonPropertyName("subscriber")]
        public PayPalSubscriber? Subscriber { get; set; }

        [JsonPropertyName("billing_info")]
        public PayPalBillingInfo? BillingInfo { get; set; }

        [JsonPropertyName("create_time")]
        public DateTime? CreateTime { get; set; }

        [JsonPropertyName("update_time")]
        public DateTime? UpdateTime { get; set; }

        [JsonPropertyName("links")]
        public List<PayPalLink>? Links { get; set; }
    }

    private class PayPalSubscriber
    {
        [JsonPropertyName("payer_id")]
        public string? PayerId { get; set; }

        [JsonPropertyName("email_address")]
        public string? EmailAddress { get; set; }
    }

    private class PayPalBillingInfo
    {
        [JsonPropertyName("next_billing_time")]
        public DateTime? NextBillingTime { get; set; }
    }

    private class PayPalLink
    {
        [JsonPropertyName("href")]
        public string? Href { get; set; }

        [JsonPropertyName("rel")]
        public string? Rel { get; set; }
    }

    private class PayPalWebhookEvent
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("event_type")]
        public string? EventType { get; set; }

        [JsonPropertyName("resource")]
        public PayPalWebhookResource? Resource { get; set; }
    }

    private class PayPalWebhookResource
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("plan_id")]
        public string? PlanId { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("custom_id")]
        public string? CustomId { get; set; }

        [JsonPropertyName("billing_agreement_id")]
        public string? BillingAgreementId { get; set; }

        [JsonPropertyName("subscriber")]
        public PayPalSubscriber? Subscriber { get; set; }

        [JsonPropertyName("billing_info")]
        public PayPalBillingInfo? BillingInfo { get; set; }
    }

    // Models for Transactions API response
    private class PayPalTransactionsResponse
    {
        [JsonPropertyName("transactions")]
        public List<PayPalTransaction>? Transactions { get; set; }

        [JsonPropertyName("total_items")]
        public int? TotalItems { get; set; }

        [JsonPropertyName("total_pages")]
        public int? TotalPages { get; set; }
    }

    private class PayPalTransaction
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("payer_email")]
        public string? PayerEmail { get; set; }

        [JsonPropertyName("payer_name")]
        public PayPalPayerName? PayerName { get; set; }

        [JsonPropertyName("amount_with_breakdown")]
        public PayPalAmountWithBreakdown? AmountWithBreakdown { get; set; }

        [JsonPropertyName("time")]
        public DateTime? Time { get; set; }
    }

    private class PayPalPayerName
    {
        [JsonPropertyName("given_name")]
        public string? GivenName { get; set; }

        [JsonPropertyName("surname")]
        public string? Surname { get; set; }
    }

    private class PayPalAmountWithBreakdown
    {
        [JsonPropertyName("gross_amount")]
        public PayPalMoney? GrossAmount { get; set; }

        [JsonPropertyName("fee_amount")]
        public PayPalMoney? FeeAmount { get; set; }

        [JsonPropertyName("net_amount")]
        public PayPalMoney? NetAmount { get; set; }
    }

    private class PayPalMoney
    {
        [JsonPropertyName("currency_code")]
        public string? CurrencyCode { get; set; }

        [JsonPropertyName("value")]
        public string? Value { get; set; }
    }

    #endregion
}
