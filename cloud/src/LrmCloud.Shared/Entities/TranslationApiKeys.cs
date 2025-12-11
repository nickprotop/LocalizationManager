using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LrmCloud.Shared.Entities;

/// <summary>
/// User-level translation provider API key and configuration.
/// Keys are encrypted at rest.
/// </summary>
[Table("user_api_keys")]
public class UserApiKey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public User? User { get; set; }

    /// <summary>
    /// Translation provider name: "google", "deepl", "openai", "azure", "ollama", etc.
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("provider")]
    public required string Provider { get; set; }

    /// <summary>
    /// AES-256 encrypted API key. Nullable for providers that don't require keys (e.g., Ollama, MyMemory).
    /// </summary>
    [Column("encrypted_key")]
    public string? EncryptedKey { get; set; }

    /// <summary>
    /// Provider-specific configuration as JSON.
    /// Contains settings like model, apiUrl, customSystemPrompt, etc.
    /// </summary>
    [Column("config_json", TypeName = "jsonb")]
    public string? ConfigJson { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Organization-level translation provider API key and configuration.
/// Shared across all organization members and projects.
/// </summary>
[Table("organization_api_keys")]
public class OrganizationApiKey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("organization_id")]
    public int OrganizationId { get; set; }

    [ForeignKey(nameof(OrganizationId))]
    public Organization? Organization { get; set; }

    /// <summary>
    /// Translation provider name: "google", "deepl", "openai", "azure", "ollama", etc.
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("provider")]
    public required string Provider { get; set; }

    /// <summary>
    /// AES-256 encrypted API key. Nullable for providers that don't require keys.
    /// </summary>
    [Column("encrypted_key")]
    public string? EncryptedKey { get; set; }

    /// <summary>
    /// Provider-specific configuration as JSON.
    /// </summary>
    [Column("config_json", TypeName = "jsonb")]
    public string? ConfigJson { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Project-level translation provider API key and configuration.
/// Highest priority in the hierarchy - overrides user and organization settings.
/// </summary>
[Table("project_api_keys")]
public class ProjectApiKey
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("project_id")]
    public int ProjectId { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }

    /// <summary>
    /// Translation provider name: "google", "deepl", "openai", "azure", "ollama", etc.
    /// </summary>
    [Required]
    [MaxLength(50)]
    [Column("provider")]
    public required string Provider { get; set; }

    /// <summary>
    /// AES-256 encrypted API key. Nullable for providers that don't require keys.
    /// </summary>
    [Column("encrypted_key")]
    public string? EncryptedKey { get; set; }

    /// <summary>
    /// Provider-specific configuration as JSON.
    /// </summary>
    [Column("config_json", TypeName = "jsonb")]
    public string? ConfigJson { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
