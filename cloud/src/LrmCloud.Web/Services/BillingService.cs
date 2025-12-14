using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Billing;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for Stripe billing operations.
/// </summary>
public class BillingService
{
    private readonly HttpClient _httpClient;

    public BillingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Get current subscription status.
    /// </summary>
    public async Task<SubscriptionDto?> GetSubscriptionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("billing/subscription");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<SubscriptionDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    /// <summary>
    /// Create checkout session for upgrading to a paid plan.
    /// </summary>
    public async Task<CheckoutSessionDto?> CreateCheckoutAsync(string plan, string successUrl, string cancelUrl)
    {
        try
        {
            var request = new CreateCheckoutRequest
            {
                Plan = plan,
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl
            };

            var response = await _httpClient.PostAsJsonAsync("billing/checkout", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<CheckoutSessionDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    /// <summary>
    /// Create portal session for managing subscription.
    /// </summary>
    public async Task<PortalSessionDto?> CreatePortalAsync(string returnUrl)
    {
        try
        {
            var request = new CreatePortalRequest { ReturnUrl = returnUrl };

            var response = await _httpClient.PostAsJsonAsync("billing/portal", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<PortalSessionDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    /// <summary>
    /// Cancel subscription at period end.
    /// </summary>
    public async Task<bool> CancelSubscriptionAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("billing/cancel", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reactivate a canceled subscription.
    /// </summary>
    public async Task<bool> ReactivateSubscriptionAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("billing/reactivate", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Get invoice history.
    /// </summary>
    public async Task<List<InvoiceDto>> GetInvoicesAsync(int limit = 10)
    {
        try
        {
            var response = await _httpClient.GetAsync($"billing/invoices?limit={limit}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<InvoiceDto>>>();
                return result?.Data ?? new List<InvoiceDto>();
            }
        }
        catch
        {
            // Ignore errors
        }
        return new List<InvoiceDto>();
    }

    /// <summary>
    /// Get current payment method.
    /// </summary>
    public async Task<PaymentMethodDto?> GetPaymentMethodAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("billing/payment-method");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<PaymentMethodDto?>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    /// <summary>
    /// Get URL to update payment method.
    /// </summary>
    public async Task<string?> GetUpdatePaymentUrlAsync(string returnUrl)
    {
        try
        {
            var response = await _httpClient.GetAsync($"billing/update-payment-url?returnUrl={Uri.EscapeDataString(returnUrl)}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }
}
