using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Billing;

/// <summary>
/// Request to create a Stripe Checkout session.
/// </summary>
public record CreateCheckoutRequest
{
    /// <summary>
    /// Plan to subscribe to: "team" or "enterprise".
    /// </summary>
    [Required]
    [RegularExpression("^(team|enterprise)$", ErrorMessage = "Plan must be 'team' or 'enterprise'")]
    public required string Plan { get; init; }

    /// <summary>
    /// URL to redirect to after successful payment.
    /// </summary>
    [Required]
    [Url]
    public required string SuccessUrl { get; init; }

    /// <summary>
    /// URL to redirect to if user cancels.
    /// </summary>
    [Required]
    [Url]
    public required string CancelUrl { get; init; }
}
