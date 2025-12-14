namespace LrmCloud.Shared.DTOs.Billing;

/// <summary>
/// Response containing a Stripe Customer Portal session URL.
/// </summary>
public record PortalSessionDto
{
    /// <summary>
    /// URL to redirect the user to for subscription management.
    /// </summary>
    public required string PortalUrl { get; init; }
}
