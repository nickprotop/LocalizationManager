namespace LocalizationManager.Models.Api;

// Request
public class MergeDuplicatesRequest
{
    public string? Key { get; set; }
    public bool MergeAll { get; set; }
    public bool AutoFirst { get; set; }
}

// Response
public class MergeDuplicatesResponse
{
    public bool Success { get; set; }
    public int MergedCount { get; set; }
    public List<string> MergedKeys { get; set; } = new();
    public string? Message { get; set; }
}

// For listing duplicates
public class DuplicateKeysResponse
{
    public List<DuplicateKeyInfo> DuplicateKeys { get; set; } = new();
    public int TotalDuplicateKeys { get; set; }
}

public class DuplicateKeyInfo
{
    public string Key { get; set; } = string.Empty;
    public int OccurrenceCount { get; set; }
    public Dictionary<string, List<string>> ValuesByLanguage { get; set; } = new();
}
