using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Billing;

/// <summary>
/// Request to create a Stripe Customer Portal session.
/// </summary>
public record CreatePortalRequest
{
    /// <summary>
    /// URL to return to after portal session.
    /// </summary>
    [Required]
    [Url]
    public required string ReturnUrl { get; init; }
}
