// Copyright (c) 2025 Nikolaos Protopapas. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Tracks processed webhook events for idempotency.
/// Prevents duplicate processing of the same webhook event.
/// </summary>
[Table("webhook_events")]
public class WebhookEvent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// The unique event ID from the payment provider.
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("provider_event_id")]
    public required string ProviderEventId { get; set; }

    /// <summary>
    /// The payment provider name (e.g., "stripe", "paypal").
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("provider_name")]
    public required string ProviderName { get; set; }

    /// <summary>
    /// The type of webhook event (e.g., "CheckoutCompleted", "SubscriptionActivated").
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("event_type")]
    public required string EventType { get; set; }

    /// <summary>
    /// The user ID associated with this webhook event (if applicable).
    /// </summary>
    [Column("user_id")]
    public int? UserId { get; set; }

    /// <summary>
    /// When the webhook was processed.
    /// </summary>
    [Column("processed_at")]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the webhook was processed successfully.
    /// </summary>
    [Column("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Error message if processing failed.
    /// </summary>
    [MaxLength(500)]
    [Column("error_message")]
    public string? ErrorMessage { get; set; }
}
