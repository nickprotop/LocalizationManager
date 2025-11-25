using LocalizationManager.Core.Configuration;

namespace LocalizationManager.Models.Api;

// Response
public class ConfigurationResponse
{
    public ConfigurationModel? Configuration { get; set; }
    public string? Message { get; set; }
}

public class ConfigValidationResponse
{
    public bool IsValid { get; set; }
    public List<string>? Errors { get; set; }
}

public class ConfigSchemaResponse
{
    public object? Schema { get; set; }
}

public class SchemaEnrichedConfigResponse
{
    public string? EnrichedJson { get; set; }
    public string? Message { get; set; }
}
