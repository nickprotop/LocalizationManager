using LocalizationManager.Core.Models;
using LocalizationManager.Core.Validation;

namespace LocalizationManager.Models.Api;

// Request
public class ValidateRequest
{
    public PlaceholderType EnabledPlaceholderTypes { get; set; } = PlaceholderType.All;
}

// Response
public class ValidationResponse
{
    public bool IsValid { get; set; }
    public Dictionary<string, List<string>> MissingKeys { get; set; } = new();
    public Dictionary<string, List<string>> DuplicateKeys { get; set; } = new();
    public Dictionary<string, List<string>> EmptyValues { get; set; } = new();
    public Dictionary<string, List<string>> ExtraKeys { get; set; } = new();
    public Dictionary<string, List<string>> PlaceholderMismatches { get; set; } = new();
    public ValidationSummary? Summary { get; set; }
}

public class ValidationSummary
{
    public int TotalIssues { get; set; }
    public int MissingCount { get; set; }
    public int DuplicatesCount { get; set; }
    public int EmptyCount { get; set; }
    public int ExtraCount { get; set; }
    public int PlaceholderCount { get; set; }
    public bool HasIssues { get; set; }
}
