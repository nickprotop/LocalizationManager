using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Request to create a new resource key.
/// </summary>
public class CreateResourceKeyRequest
{
    [Required(ErrorMessage = "Key name is required")]
    [MaxLength(500, ErrorMessage = "Key name must not exceed 500 characters")]
    public required string KeyName { get; set; }

    [MaxLength(500, ErrorMessage = "Key path must not exceed 500 characters")]
    public string? KeyPath { get; set; }

    public bool IsPlural { get; set; }

    [MaxLength(1000, ErrorMessage = "Comment must not exceed 1000 characters")]
    public string? Comment { get; set; }

    /// <summary>
    /// Initial translation value in the default language (optional).
    /// </summary>
    public string? DefaultLanguageValue { get; set; }
}
