using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Projects;

/// <summary>
/// Request to create a new project.
/// </summary>
public class CreateProjectRequest
{
    /// <summary>
    /// URL-friendly identifier (no spaces, lowercase, used in remote URLs).
    /// Must contain only lowercase letters, numbers, and hyphens.
    /// </summary>
    [Required(ErrorMessage = "Project slug is required")]
    [MaxLength(100, ErrorMessage = "Project slug must not exceed 100 characters")]
    [RegularExpression(@"^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$",
        ErrorMessage = "Slug must contain only lowercase letters, numbers, and hyphens, and cannot start or end with a hyphen")]
    public required string Slug { get; set; }

    /// <summary>
    /// Display name for the project (can contain spaces and special characters).
    /// </summary>
    [Required(ErrorMessage = "Project name is required")]
    [MaxLength(255, ErrorMessage = "Project name must not exceed 255 characters")]
    public required string Name { get; set; }

    [MaxLength(1000, ErrorMessage = "Description must not exceed 1000 characters")]
    public string? Description { get; set; }

    /// <summary>
    /// Organization ID (if creating an organization project).
    /// If null, creates a personal project for the current user.
    /// </summary>
    public int? OrganizationId { get; set; }

    [Required(ErrorMessage = "Format is required")]
    [MaxLength(50, ErrorMessage = "Format must not exceed 50 characters")]
    public required string Format { get; set; }  // resx, json, i18next

    [MaxLength(10, ErrorMessage = "Default language must not exceed 10 characters")]
    public string DefaultLanguage { get; set; } = "en";

    [MaxLength(500, ErrorMessage = "Localization path must not exceed 500 characters")]
    public string LocalizationPath { get; set; } = ".";

    // GitHub integration (optional)
    [MaxLength(255, ErrorMessage = "GitHub repo must not exceed 255 characters")]
    public string? GitHubRepo { get; set; }

    [MaxLength(100, ErrorMessage = "GitHub default branch must not exceed 100 characters")]
    public string? GitHubDefaultBranch { get; set; }
}
