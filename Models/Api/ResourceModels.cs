namespace LocalizationManager.Models.Api;

// Responses
public class ResourceFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? Code { get; set; }
    public bool IsDefault { get; set; }
}

public class ResourceKeyInfo
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, string?> Values { get; set; } = new();
    public int OccurrenceCount { get; set; } = 1;
    public bool HasDuplicates { get; set; }
}

public class ResourceKeyDetails
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, ResourceValue> Values { get; set; } = new();
    public int OccurrenceCount { get; set; } = 1;
    public bool HasDuplicates { get; set; }
    public List<DuplicateOccurrence>? Occurrences { get; set; }
}

public class ResourceValue
{
    public string? Value { get; set; }
    public string? Comment { get; set; }
}

public class DuplicateOccurrence
{
    public int OccurrenceNumber { get; set; }
    public Dictionary<string, ResourceValue> Values { get; set; } = new();
}

// Requests
public class AddKeyRequest
{
    public string Key { get; set; } = string.Empty;
    public Dictionary<string, string>? Values { get; set; }
    public string? Comment { get; set; }
}

public class UpdateKeyRequest
{
    public Dictionary<string, string>? Values { get; set; }
    public string? Comment { get; set; }
    public int? Occurrence { get; set; }
}

// Common response models
public class OperationResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class DeleteKeyResponse
{
    public bool Success { get; set; }
    public string Key { get; set; } = string.Empty;
    public int DeletedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
}
