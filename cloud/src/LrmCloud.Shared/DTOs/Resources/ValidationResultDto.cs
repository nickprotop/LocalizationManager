using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Result of validating a project's resources.
/// </summary>
public class ValidationResultDto
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
}

/// <summary>
/// A validation issue found in a project.
/// </summary>
public class ValidationIssue
{
    [Required]
    public string Severity { get; set; } = "warning";  // error, warning, info

    [Required]
    public required string Message { get; set; }

    public string? KeyName { get; set; }
    public string? LanguageCode { get; set; }
}
