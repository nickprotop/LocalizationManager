namespace LrmCloud.Shared.DTOs.Billing;

/// <summary>
/// Invoice data transfer object for frontend display.
/// </summary>
public record InvoiceDto
{
    /// <summary>
    /// The invoice ID from the payment provider.
    /// </summary>
    public string InvoiceId { get; init; } = "";

    /// <summary>
    /// Invoice number for display (may differ from ID).
    /// </summary>
    public string? InvoiceNumber { get; init; }

    /// <summary>
    /// The invoice status (draft, open, paid, void, uncollectible).
    /// </summary>
    public string Status { get; init; } = "";

    /// <summary>
    /// Total amount in cents/smallest currency unit.
    /// </summary>
    public long AmountTotal { get; init; }

    /// <summary>
    /// Amount paid in cents/smallest currency unit.
    /// </summary>
    public long AmountPaid { get; init; }

    /// <summary>
    /// Currency code (e.g., "usd", "eur").
    /// </summary>
    public string Currency { get; init; } = "usd";

    /// <summary>
    /// When the invoice was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

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
