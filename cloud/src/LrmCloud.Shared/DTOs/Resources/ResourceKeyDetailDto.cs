namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Detailed resource key with all translations.
/// </summary>
public class ResourceKeyDetailDto : ResourceKeyDto
{
    public List<TranslationDto> Translations { get; set; } = new();
}
