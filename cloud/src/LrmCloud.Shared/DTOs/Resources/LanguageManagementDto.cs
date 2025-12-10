namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Information about a language in a project.
/// </summary>
public class ProjectLanguageDto
{
    public string LanguageCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsDefault { get; set; }
    public int TranslatedCount { get; set; }
    public int TotalKeys { get; set; }
    public double CompletionPercentage { get; set; }
    public DateTime? LastUpdated { get; set; }
}

/// <summary>
/// Request to add a new language to a project.
/// </summary>
public class AddLanguageRequest
{
    public string LanguageCode { get; set; } = "";
}

/// <summary>
/// Request to remove a language from a project.
/// </summary>
public class RemoveLanguageRequest
{
    public string LanguageCode { get; set; } = "";
    public bool ConfirmDelete { get; set; }
}
