namespace LocalizationManager.Models.Api;

// Request
public class ScanRequest
{
    public List<string>? ExcludePatterns { get; set; }
}

// Response
public class ScanResponse
{
    public int ScannedFiles { get; set; }
    public int TotalReferences { get; set; }
    public int UniqueKeysFound { get; set; }
    public int UnusedKeysCount { get; set; }
    public int MissingKeysCount { get; set; }
    public List<string> Unused { get; set; } = new();
    public List<string> Missing { get; set; } = new();
    public List<KeyReferenceInfo> References { get; set; } = new();
}

public class KeyReferenceInfo
{
    public string Key { get; set; } = string.Empty;
    public int ReferenceCount { get; set; }
    public List<CodeReference> References { get; set; } = new();
}

public class CodeReference
{
    public string File { get; set; } = string.Empty;
    public int Line { get; set; }
    public string Pattern { get; set; } = string.Empty;
    public string Confidence { get; set; } = string.Empty;
}

public class UnusedKeysResponse
{
    public List<string> UnusedKeys { get; set; } = new();
}

public class MissingKeysResponse
{
    public List<string> MissingKeys { get; set; } = new();
}

public class KeyReferencesResponse
{
    public string Key { get; set; } = string.Empty;
    public int ReferenceCount { get; set; }
    public List<CodeReference> References { get; set; } = new();
}
