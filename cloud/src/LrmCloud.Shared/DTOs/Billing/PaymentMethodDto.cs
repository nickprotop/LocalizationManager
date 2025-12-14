namespace LrmCloud.Shared.DTOs.Billing;

/// <summary>
/// Payment method data transfer object for frontend display.
/// </summary>
public record PaymentMethodDto
{
    /// <summary>
    /// The payment method ID from the provider.
    /// </summary>
    public string PaymentMethodId { get; init; } = "";

    /// <summary>
    /// The type of payment method (card, paypal, bank_account, unknown).
    /// </summary>
    public string Type { get; init; } = "";

    /// <summary>
    /// Card brand (for card payments): visa, mastercard, amex, etc.
    /// </summary>
    public string? CardBrand { get; init; }

    /// <summary>
    /// Last 4 digits of the card (for card payments).
    /// </summary>
    public string? CardLast4 { get; init; }

    /// <summary>
    /// Card expiration month (1-12).
    /// </summary>
    public int? CardExpMonth { get; init; }

    /// <summary>
    /// Card expiration year (4 digits).
    /// </summary>
    public int? CardExpYear { get; init; }

    /// <summary>
    /// PayPal email address (for PayPal payments).
    /// </summary>
    public string? PayPalEmail { get; init; }

    /// <summary>
    /// Bank name (for bank account payments).
    /// </summary>
    public string? BankName { get; init; }

    /// <summary>
    /// Last 4 digits of bank account (for bank payments).
    /// </summary>
    public string? BankLast4 { get; init; }

    /// <summary>
    /// Whether this is the default payment method.
    /// </summary>
    public bool IsDefault { get; init; }

    /// <summary>
    /// Pre-formatted display string for the payment method.
    /// </summary>
    public string DisplayString { get; init; } = "";
}
