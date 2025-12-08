using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Single translation update in a bulk operation.
/// </summary>
public class BulkTranslationUpdate
{
    [Required(ErrorMessage = "Key name is required")]
    public required string KeyName { get; set; }

    [Required(ErrorMessage = "Value is required")]
    public required string Value { get; set; }

    public string PluralForm { get; set; } = "";

    public string Status { get; set; } = "translated";
}
