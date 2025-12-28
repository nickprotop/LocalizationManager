namespace LrmCloud.Api.Services.Billing.Models;

/// <summary>
/// Provider-agnostic payment method information for the custom billing portal.
/// </summary>
public sealed class PaymentMethodInfo
{
    /// <summary>
    /// The payment method ID from the provider.
    /// </summary>
    public required string PaymentMethodId { get; init; }

    /// <summary>
    /// The type of payment method.
    /// </summary>
    public required PaymentMethodType Type { get; init; }

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
    /// Display string for the payment method.
    /// </summary>
    public string DisplayString => Type switch
    {
        PaymentMethodType.Card => $"{CardBrand?.ToUpperInvariant() ?? "Card"} •••• {CardLast4}",
        PaymentMethodType.PayPal => "PayPal",
        PaymentMethodType.BankAccount => $"{BankName ?? "Bank"} •••• {BankLast4}",
        _ => "Unknown payment method"
    };
}

/// <summary>
/// Types of payment methods supported.
/// </summary>
public enum PaymentMethodType
{
    /// <summary>
    /// Unknown payment method type.
    /// </summary>
    Unknown,

    /// <summary>
    /// Credit or debit card.
    /// </summary>
    Card,

    /// <summary>
    /// PayPal account.
    /// </summary>
    PayPal,

    /// <summary>
    /// Bank account (ACH, SEPA, etc.).
    /// </summary>
    BankAccount
}
