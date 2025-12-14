namespace LrmCloud.Shared.DTOs.Billing;

/// <summary>
/// Response containing a Stripe Checkout session URL.
/// </summary>
public record CheckoutSessionDto
{
    /// <summary>
    /// URL to redirect the user to for payment.
    /// </summary>
    public required string SessionUrl { get; init; }

    /// <summary>
    /// Checkout session ID (for reference).
    /// </summary>
    public string? SessionId { get; init; }
}
