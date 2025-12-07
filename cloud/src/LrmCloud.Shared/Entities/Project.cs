using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// Localization project entity.
/// Can belong to either a user or an organization.
/// </summary>
[Table("projects")]
public class Project
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    [Column("organization_id")]
    public int? OrganizationId { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }

    [Required]
    [MaxLength(255)]
    [Column("name")]
    public required string Name { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    // GitHub integration
    [MaxLength(255)]
    [Column("github_repo")]
    public string? GitHubRepo { get; set; }

    [Column("github_installation_id")]
    public long? GitHubInstallationId { get; set; }

    [MaxLength(100)]
    [Column("github_default_branch")]
    public string GitHubDefaultBranch { get; set; } = "main";

    [MaxLength(255)]
    [Column("github_webhook_secret")]
    public string? GitHubWebhookSecret { get; set; }

    // Localization settings
    [MaxLength(500)]
    [Column("localization_path")]
    public string LocalizationPath { get; set; } = ".";

    [Required]
    [MaxLength(50)]
    [Column("format")]
    public required string Format { get; set; } // resx, json, i18next

    [MaxLength(10)]
    [Column("default_language")]
    public string DefaultLanguage { get; set; } = "en";

    // Sync settings
    [MaxLength(50)]
    [Column("sync_mode")]
    public string SyncMode { get; set; } = "manual";

    [Column("auto_translate")]
    public bool AutoTranslate { get; set; }

    [Column("auto_create_pr")]
    public bool AutoCreatePr { get; set; } = true;

    // State
    [Column("last_synced_at")]
    public DateTime? LastSyncedAt { get; set; }

    [MaxLength(40)]
    [Column("last_synced_commit")]
    public string? LastSyncedCommit { get; set; }

    [MaxLength(50)]
    [Column("sync_status")]
    public string SyncStatus { get; set; } = "pending";

    [Column("sync_error")]
    public string? SyncError { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ResourceKey> ResourceKeys { get; set; } = new List<ResourceKey>();
    public ICollection<ProjectApiKey> ProjectApiKeys { get; set; } = new List<ProjectApiKey>();
    public ICollection<SyncHistory> SyncHistory { get; set; } = new List<SyncHistory>();
}
