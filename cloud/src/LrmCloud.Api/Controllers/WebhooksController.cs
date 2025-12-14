using LrmCloud.Api.Services;
using LrmCloud.Api.Services.Billing;
using Microsoft.AspNetCore.Mvc;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// Webhook endpoints for external services (payment providers, etc.).
/// </summary>
[Route("api/webhooks")]
[ApiController]
public class WebhooksController : ControllerBase
{
    private readonly IBillingService _billingService;
    private readonly PaymentProviderFactory _providerFactory;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IBillingService billingService,
        PaymentProviderFactory providerFactory,
        ILogger<WebhooksController> logger)
    {
        _billingService = billingService;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    /// Stripe webhook handler.
    /// Receives and processes Stripe webhook events.
    /// </summary>
    /// <remarks>
    /// This endpoint does not use JWT authentication.
    /// Instead, it uses Stripe signature verification to validate requests.
    /// </remarks>
    [HttpPost("stripe")]
    public Task<IActionResult> StripeWebhook()
    {
        return HandleWebhookAsync("stripe", "Stripe-Signature");
    }

    /// <summary>
    /// PayPal webhook handler.
    /// Receives and processes PayPal webhook events.
    /// </summary>
    /// <remarks>
    /// This endpoint does not use JWT authentication.
    /// Instead, it uses PayPal signature verification to validate requests.
    /// </remarks>
    [HttpPost("paypal")]
    public Task<IActionResult> PayPalWebhook()
    {
        return HandleWebhookAsync("paypal", "PAYPAL-TRANSMISSION-SIG");
    }

    /// <summary>
    /// Generic webhook handler that delegates to the appropriate payment provider.
    /// </summary>
    private async Task<IActionResult> HandleWebhookAsync(string providerName, string signatureHeader)
    {
        if (!_billingService.IsEnabled)
        {
            _logger.LogWarning("{Provider} webhook received but billing is not enabled", providerName);
            return BadRequest("Billing is not enabled");
        }

        // Check if provider is registered
        if (!_providerFactory.TryGetProvider(providerName, out var provider) || provider == null)
        {
            _logger.LogWarning("{Provider} webhook received but provider is not registered", providerName);
            return BadRequest($"Provider '{providerName}' is not registered");
        }

        if (!provider.IsEnabled)
        {
            _logger.LogWarning("{Provider} webhook received but provider is not enabled", providerName);
            return BadRequest($"Provider '{providerName}' is not enabled");
        }

        // Read the raw body for signature verification
        string payload;
        using (var reader = new StreamReader(Request.Body))
        {
            payload = await reader.ReadToEndAsync();
        }

        // Get the signature header
        var signature = Request.Headers[signatureHeader].FirstOrDefault();

        if (string.IsNullOrEmpty(signature))
        {
            _logger.LogWarning("{Provider} webhook received without signature header ({Header})",
                providerName, signatureHeader);
            return BadRequest($"Missing {signatureHeader} header");
        }

        try
        {
            // Process webhook with provider
            var result = await provider.ProcessWebhookAsync(payload, signature);

            if (!result.Success)
            {
                _logger.LogWarning("{Provider} webhook processing failed: {Error}",
                    providerName, result.ErrorMessage);
                return BadRequest(result.ErrorMessage);
            }

            // Handle the webhook result (update database, etc.)
            await _billingService.HandleWebhookResultAsync(result);

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {Provider} webhook", providerName);
            // Return 200 to prevent provider from retrying for unhandled errors
            // Log the error for investigation
            return Ok();
        }
    }
}
