namespace LrmCloud.Api.Services.Billing.Models;

/// <summary>
/// Provider-agnostic invoice information for the custom billing portal.
/// </summary>
public sealed class InvoiceInfo
{
    /// <summary>
    /// The invoice ID from the payment provider.
    /// </summary>
    public required string InvoiceId { get; init; }

    /// <summary>
    /// Invoice number for display (may differ from ID).
    /// </summary>
    public string? InvoiceNumber { get; init; }

    /// <summary>
    /// The invoice status.
    /// </summary>
    public required InvoiceStatus Status { get; init; }

    /// <summary>
    /// Total amount in cents/smallest currency unit.
    /// </summary>
    public required long AmountTotal { get; init; }

    /// <summary>
    /// Amount paid in cents/smallest currency unit.
    /// </summary>
    public long AmountPaid { get; init; }

    /// <summary>
    /// Currency code (e.g., "usd", "eur").
    /// </summary>
    public required string Currency { get; init; }

    /// <summary>
    /// When the invoice was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the invoice was paid (if applicable).
    /// </summary>
    public DateTime? PaidAt { get; init; }

    /// <summary>
    /// When the invoice is due (if applicable).
    /// </summary>
    public DateTime? DueDate { get; init; }

    /// <summary>
    /// URL to download the invoice PDF (if available).
    /// </summary>
    public string? PdfUrl { get; init; }

    /// <summary>
    /// URL to view the invoice in the provider's hosted page (if available).
    /// </summary>
    public string? HostedUrl { get; init; }

    /// <summary>
    /// Description or memo on the invoice.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Billing period start (if subscription invoice).
    /// </summary>
    public DateTime? PeriodStart { get; init; }

    /// <summary>
    /// Billing period end (if subscription invoice).
    /// </summary>
    public DateTime? PeriodEnd { get; init; }
}

/// <summary>
/// Normalized invoice status across payment providers.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>
    /// Invoice is a draft and not yet finalized.
    /// </summary>
    Draft,

    /// <summary>
    /// Invoice is open and awaiting payment.
    /// </summary>
    Open,

    /// <summary>
    /// Invoice has been paid.
    /// </summary>
    Paid,

    /// <summary>
    /// Invoice has been voided/canceled.
    /// </summary>
    Void,

    /// <summary>
    /// Invoice is uncollectible.
    /// </summary>
    Uncollectible
}
