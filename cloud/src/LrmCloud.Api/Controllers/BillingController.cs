using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LrmCloud.Api.Controllers;

/// <summary>
/// API endpoints for Stripe billing operations.
/// </summary>
[Route("api/billing")]
[Authorize]
public class BillingController : ApiControllerBase
{
    private readonly IBillingService _billingService;
    private readonly ILogger<BillingController> _logger;

    public BillingController(IBillingService billingService, ILogger<BillingController> logger)
    {
        _billingService = billingService;
        _logger = logger;
    }

    private int GetUserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Get the current user's subscription status.
    /// </summary>
    [HttpGet("subscription")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> GetSubscription()
    {
        var userId = GetUserId();
        var subscription = await _billingService.GetSubscriptionAsync(userId);

        if (subscription == null)
        {
            return NotFound("billing-not-found", "No subscription found");
        }

        return Success(subscription);
    }

    /// <summary>
    /// Create a Stripe Checkout session for upgrading to a paid plan.
    /// </summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<ApiResponse<CheckoutSessionDto>>> CreateCheckout([FromBody] CreateCheckoutRequest request)
    {
        if (!_billingService.IsEnabled)
        {
            return BadRequest("billing-disabled", "Billing is not enabled");
        }

        if (request.Plan != "team" && request.Plan != "enterprise")
        {
            return BadRequest("invalid-plan", "Plan must be 'team' or 'enterprise'");
        }

        var userId = GetUserId();

        try
        {
            var checkoutUrl = await _billingService.CreateCheckoutSessionAsync(
                userId,
                request.Plan,
                request.SuccessUrl,
                request.CancelUrl);

            _logger.LogInformation("Created checkout session for user {UserId}, plan {Plan}", userId, request.Plan);

            return Success(new CheckoutSessionDto { SessionUrl = checkoutUrl });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create checkout session for user {UserId}", userId);
            return BadRequest("checkout-failed", "Failed to create checkout session");
        }
    }

    /// <summary>
    /// Create a Stripe Customer Portal session for managing subscription.
    /// </summary>
    [HttpPost("portal")]
    public async Task<ActionResult<ApiResponse<PortalSessionDto>>> CreatePortal([FromBody] CreatePortalRequest request)
    {
        if (!_billingService.IsEnabled)
        {
            return BadRequest("billing-disabled", "Billing is not enabled");
        }

        var userId = GetUserId();

        try
        {
            var portalUrl = await _billingService.CreatePortalSessionAsync(userId, request.ReturnUrl);

            return Success(new PortalSessionDto { PortalUrl = portalUrl });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Portal session failed for user {UserId}", userId);
            return BadRequest("portal-failed", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create portal session for user {UserId}", userId);
            return BadRequest("portal-failed", "Failed to create portal session");
        }
    }

    /// <summary>
    /// Cancel the current subscription (at period end).
    /// </summary>
    [HttpPost("cancel")]
    public async Task<ActionResult<ApiResponse>> CancelSubscription()
    {
        if (!_billingService.IsEnabled)
        {
            return BadRequest("billing-disabled", "Billing is not enabled");
        }

        var userId = GetUserId();

        try
        {
            await _billingService.CancelSubscriptionAsync(userId);
            _logger.LogInformation("Subscription cancellation requested for user {UserId}", userId);

            return Success("Subscription will be canceled at the end of the billing period");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cancel subscription failed for user {UserId}", userId);
            return BadRequest("cancel-failed", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel subscription for user {UserId}", userId);
            return BadRequest("cancel-failed", "Failed to cancel subscription");
        }
    }

    /// <summary>
    /// Reactivate a canceled subscription (before period end).
    /// </summary>
    [HttpPost("reactivate")]
    public async Task<ActionResult<ApiResponse>> ReactivateSubscription()
    {
        if (!_billingService.IsEnabled)
        {
            return BadRequest("billing-disabled", "Billing is not enabled");
        }

        var userId = GetUserId();

        try
        {
            await _billingService.ReactivateSubscriptionAsync(userId);
            _logger.LogInformation("Subscription reactivated for user {UserId}", userId);

            return Success("Subscription has been reactivated");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Reactivate subscription failed for user {UserId}", userId);
            return BadRequest("reactivate-failed", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reactivate subscription for user {UserId}", userId);
            return BadRequest("reactivate-failed", "Failed to reactivate subscription");
        }
    }
}
