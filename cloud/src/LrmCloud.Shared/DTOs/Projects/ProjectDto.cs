namespace LrmCloud.Shared.DTOs.Projects;

/// <summary>
/// Project data transfer object.
/// </summary>
public class ProjectDto
{
    public int Id { get; set; }

    /// <summary>
    /// URL-friendly identifier (no spaces, lowercase, used in remote URLs).
    /// </summary>
    public required string Slug { get; set; }

    /// <summary>
    /// Display name for the project.
    /// </summary>
    public required string Name { get; set; }

    public string? Description { get; set; }

    public int? UserId { get; set; }
    public int? OrganizationId { get; set; }
    public string? OrganizationName { get; set; }

    public required string Format { get; set; }
    public string DefaultLanguage { get; set; } = "en";
    public string LocalizationPath { get; set; } = ".";

    public string? GitHubRepo { get; set; }
    public string? GitHubDefaultBranch { get; set; }

    public bool AutoTranslate { get; set; }
    public bool AutoCreatePr { get; set; }

    /// <summary>
    /// Whether this project inherits glossary terms from the organization.
    /// Only applies to organization projects.
    /// </summary>
    public bool InheritOrganizationGlossary { get; set; } = true;

    public string SyncStatus { get; set; } = "idle";
    public string? SyncError { get; set; }
    public DateTime? LastSyncedAt { get; set; }

    public int KeyCount { get; set; }
    public int TranslationCount { get; set; }
    public double CompletionPercentage { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Number of validation errors (from cached validation).
    /// </summary>
    public int ValidationErrors { get; set; }

    /// <summary>
    /// Number of validation warnings (from cached validation).
    /// </summary>
    public int ValidationWarnings { get; set; }
}
