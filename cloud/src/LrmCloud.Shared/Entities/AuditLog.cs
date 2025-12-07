using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Net;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Audit log for tracking all changes in the system.
/// Essential for SOC2 compliance and debugging.
/// </summary>
[Table("audit_log")]
public class AuditLog
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Column("project_id")]
    public int? ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// Action performed: "user.login", "project.create", "translation.update", etc.
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("action")]
    public required string Action { get; set; }

    /// <summary>
    /// Entity type affected: "user", "project", "resource_key", "translation".
    /// </summary>
    [MaxLength(50)]
    [Column("entity_type")]
    public string? EntityType { get; set; }

    [Column("entity_id")]
    public int? EntityId { get; set; }

    /// <summary>
    /// Previous value as JSON (for updates).
    /// </summary>
    [Column("old_value", TypeName = "jsonb")]
    public string? OldValue { get; set; }

    /// <summary>
    /// New value as JSON (for creates/updates).
    /// </summary>
    [Column("new_value", TypeName = "jsonb")]
    public string? NewValue { get; set; }

    /// <summary>
    /// IP address of the request.
    /// </summary>
    [MaxLength(45)]
    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
