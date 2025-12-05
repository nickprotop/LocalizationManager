using System.ComponentModel.DataAnnotations;

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
    public bool IsPlural { get; set; }
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
    public bool IsPlural { get; set; }
    public Dictionary<string, string>? PluralForms { get; set; }
}

public class DuplicateOccurrence
{
    public int OccurrenceNumber { get; set; }
    public Dictionary<string, ResourceValue> Values { get; set; } = new();
}

// Requests
public class AddKeyRequest
{
    [Required(ErrorMessage = "Key is required")]
    [StringLength(500, MinimumLength = 1, ErrorMessage = "Key must be between 1 and 500 characters")]
    [RegularExpression(@"^[\w\.\-\[\]\s]+$", ErrorMessage = "Key contains invalid characters")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Simple string values per language (for non-plural keys).
    /// </summary>
    public Dictionary<string, string>? Values { get; set; }

    /// <summary>
    /// Whether this key is a plural form.
    /// </summary>
    public bool IsPlural { get; set; }

    /// <summary>
    /// Plural form values per language (for plural keys).
    /// Key is language code, value is dictionary of plural forms (one, other, zero, etc.).
    /// </summary>
    public Dictionary<string, Dictionary<string, string>>? PluralValues { get; set; }

    [StringLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters")]
    public string? Comment { get; set; }
}

public class UpdateKeyRequest
{
    /// <summary>
    /// Values per language. Each entry can contain a value and optionally a comment.
    /// </summary>
    public Dictionary<string, ResourceValue>? Values { get; set; }

    /// <summary>
    /// Global comment to apply to all languages (used as fallback if per-language comment not provided).
    /// </summary>
    [StringLength(2000, ErrorMessage = "Comment cannot exceed 2000 characters")]
    public string? Comment { get; set; }

    [Range(1, 100, ErrorMessage = "Occurrence must be between 1 and 100")]
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
