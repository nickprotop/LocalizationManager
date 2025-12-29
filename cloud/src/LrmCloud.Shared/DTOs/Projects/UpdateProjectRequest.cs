using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Projects;

/// <summary>
/// Request to update a project.
/// Note: Format is now managed per-GitHub connection only (GitHubFormat).
/// </summary>
public class UpdateProjectRequest
{
    /// <summary>
    /// URL-friendly identifier (no spaces, lowercase, used in remote URLs).
    /// Must contain only lowercase letters, numbers, and hyphens.
    /// </summary>
    [MaxLength(100, ErrorMessage = "Project slug must not exceed 100 characters")]
    [RegularExpression(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$",
        ErrorMessage = "Slug must contain only lowercase letters, numbers, and hyphens, and cannot start or end with a hyphen")]
    public string? Slug { get; set; }

    /// <summary>
    /// Display name for the project.
    /// </summary>
    [MaxLength(255, ErrorMessage = "Project name must not exceed 255 characters")]
    public string? Name { get; set; }

    [MaxLength(1000, ErrorMessage = "Description must not exceed 1000 characters")]
    public string? Description { get; set; }

    // Note: DefaultLanguage is immutable - cannot be changed after creation

    [MaxLength(255, ErrorMessage = "GitHub repo must not exceed 255 characters")]
    public string? GitHubRepo { get; set; }

    [MaxLength(100, ErrorMessage = "GitHub default branch must not exceed 100 characters")]
    public string? GitHubDefaultBranch { get; set; }

    /// <summary>
    /// Path in the GitHub repo where resource files are located.
    /// </summary>
    [MaxLength(500, ErrorMessage = "GitHub base path must not exceed 500 characters")]
    public string? GitHubBasePath { get; set; }

    /// <summary>
    /// Resource format for GitHub operations (null = auto-detect).
    /// </summary>
    [MaxLength(50, ErrorMessage = "GitHub format must not exceed 50 characters")]
    public string? GitHubFormat { get; set; }

    public bool? AutoTranslate { get; set; }

    public bool? AutoCreatePr { get; set; }

    /// <summary>
    /// Organization ID to transfer/assign this project to.
    /// Set to null to make it a personal project.
    /// Set to a valid organization ID to transfer it to that organization.
    /// </summary>
    public int? OrganizationId { get; set; }

    /// <summary>
    /// Flag to indicate we want to explicitly set the OrganizationId (even to null).
    /// Required because OrganizationId being null could mean "don't change" or "remove from org".
    /// </summary>
    public bool UpdateOrganization { get; set; }

    /// <summary>
    /// Whether this project inherits glossary terms from the organization.
    /// Only applies to organization projects.
    /// </summary>
    public bool? InheritOrganizationGlossary { get; set; }
}
