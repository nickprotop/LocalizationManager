namespace LocalizationManager.Models.Api;

// Request
public class TranslateRequest
{
    public string? Provider { get; set; }
    public string? SourceLanguage { get; set; }
    public List<string>? TargetLanguages { get; set; }
    public List<string>? Keys { get; set; }
    public bool OnlyMissing { get; set; }
    public bool DryRun { get; set; }
}

// Response
public class TranslationResponse
{
    public bool Success { get; set; }
    public int TranslatedCount { get; set; }
    public int ErrorCount { get; set; }
    public List<TranslationResult> Results { get; set; } = new();
    public List<TranslationError> Errors { get; set; } = new();
    public bool DryRun { get; set; }
}

public class TranslationResult
{
    public string Key { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string? TranslatedValue { get; set; }
    public bool Success { get; set; }
}

public class TranslationError
{
    public string Key { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public class TranslationProvidersResponse
{
    public List<TranslationProviderInfo> Providers { get; set; } = new();
}

public class TranslationProviderInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool RequiresApiKey { get; set; }
}
