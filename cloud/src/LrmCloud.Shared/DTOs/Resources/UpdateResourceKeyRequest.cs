using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Request to update a resource key.
/// </summary>
public class UpdateResourceKeyRequest
{
    [MaxLength(500, ErrorMessage = "Key path must not exceed 500 characters")]
    public string? KeyPath { get; set; }

    public bool? IsPlural { get; set; }

    [MaxLength(1000, ErrorMessage = "Comment must not exceed 1000 characters")]
    public string? Comment { get; set; }
}
