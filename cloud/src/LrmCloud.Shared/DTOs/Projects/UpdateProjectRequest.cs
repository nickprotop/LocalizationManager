using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Projects;

/// <summary>
/// Request to update a project.
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

    [MaxLength(50, ErrorMessage = "Format must not exceed 50 characters")]
    public string? Format { get; set; }

    [MaxLength(10, ErrorMessage = "Default language must not exceed 10 characters")]
    public string? DefaultLanguage { get; set; }

    [MaxLength(500, ErrorMessage = "Localization path must not exceed 500 characters")]
    public string? LocalizationPath { get; set; }

    [MaxLength(255, ErrorMessage = "GitHub repo must not exceed 255 characters")]
    public string? GitHubRepo { get; set; }

    [MaxLength(100, ErrorMessage = "GitHub default branch must not exceed 100 characters")]
    public string? GitHubDefaultBranch { get; set; }

    public bool? AutoTranslate { get; set; }

    public bool? AutoCreatePr { get; set; }
}
