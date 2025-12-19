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

    /// <summary>
    /// URL-friendly identifier (no spaces, lowercase, used in remote URLs).
    /// Example: "my-project" for URL like /@username/my-project
    /// </summary>
    [Required]
    [MaxLength(100)]
    [Column("slug")]
    public required string Slug { get; set; }

    /// <summary>
    /// Display name for the project (can contain spaces and special characters).
    /// </summary>
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

    // Configuration storage (lrm.json)
    [Column("config_json", TypeName = "jsonb")]
    public string? ConfigJson { get; set; }

    [MaxLength(40)]
    [Column("config_version")]
    public string? ConfigVersion { get; set; }

    [Column("config_updated_at")]
    public DateTime? ConfigUpdatedAt { get; set; }

    [Column("config_updated_by")]
    public int? ConfigUpdatedBy { get; set; }

    [ForeignKey(nameof(ConfigUpdatedBy))]
    public User? ConfigUpdater { get; set; }

    // Glossary settings
    /// <summary>
    /// Whether this project inherits glossary terms from the organization.
    /// Only applies to organization projects. Default is true for new projects.
    /// </summary>
    [Column("inherit_organization_glossary")]
    public bool InheritOrganizationGlossary { get; set; } = true;

    // Review workflow settings
    /// <summary>
    /// Enable the review workflow for this project.
    /// When false, all workflow features are disabled.
    /// </summary>
    [Column("review_workflow_enabled")]
    public bool ReviewWorkflowEnabled { get; set; } = false;

    /// <summary>
    /// When enabled, translations must be reviewed before export/sync.
    /// </summary>
    [Column("require_review_before_export")]
    public bool RequireReviewBeforeExport { get; set; } = false;

    /// <summary>
    /// When enabled, translations must be approved (after review) before export/sync.
    /// </summary>
    [Column("require_approval_before_export")]
    public bool RequireApprovalBeforeExport { get; set; } = false;

    /// <summary>
    /// Whether this project inherits reviewers from the organization.
    /// Only applies to organization projects.
    /// </summary>
    [Column("inherit_organization_reviewers")]
    public bool InheritOrganizationReviewers { get; set; } = true;

    // Snapshot settings
    /// <summary>
    /// Number of days to retain snapshots. Null means keep forever.
    /// </summary>
    [Column("snapshot_retention_days")]
    public int? SnapshotRetentionDays { get; set; }

    /// <summary>
    /// Maximum number of snapshots to keep. Null means unlimited.
    /// </summary>
    [Column("max_snapshots")]
    public int? MaxSnapshots { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<ResourceKey> ResourceKeys { get; set; } = new List<ResourceKey>();
    public ICollection<ProjectApiKey> ProjectApiKeys { get; set; } = new List<ProjectApiKey>();
    public ICollection<SyncHistory> SyncHistory { get; set; } = new List<SyncHistory>();
    public ICollection<Snapshot> Snapshots { get; set; } = new List<Snapshot>();
    public ICollection<ProjectReviewer> Reviewers { get; set; } = new List<ProjectReviewer>();
}
