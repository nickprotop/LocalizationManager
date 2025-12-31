namespace LrmCloud.Api.Services;

/// <summary>
/// Represents a parsed entry from a GitHub file.
/// </summary>
public class GitHubEntry
{
    public required string Key { get; init; }
    public required string LanguageCode { get; init; }
    public string PluralForm { get; init; } = "";
    public string? Value { get; init; }
    public string? Comment { get; init; }
    public bool IsPlural { get; init; }
    public Dictionary<string, string>? PluralForms { get; init; }
    /// <summary>
    /// For plural keys, the source plural text pattern (PO msgid_plural or "other" form).
    /// </summary>
    public string? SourcePluralText { get; init; }
    public required string Hash { get; init; }
}

/// <summary>
/// Result of parsing a single file.
/// </summary>
public class ParsedResourceFile
{
    public required string FilePath { get; init; }
    public required string LanguageCode { get; init; }
    public bool IsDefault { get; init; }
    public required List<GitHubEntry> Entries { get; init; }
}

/// <summary>
/// Result of parsing multiple files.
/// </summary>
public class ParseFilesResult
{
    public Dictionary<(string Key, string LanguageCode, string PluralForm), GitHubEntry> Entries { get; init; } = new();
    public List<FileParseErrorInfo> ParseErrors { get; init; } = new();
}

/// <summary>
/// Information about a file that failed to parse.
/// </summary>
public class FileParseErrorInfo
{
    public required string Path { get; init; }
    public required string Error { get; init; }
}

/// <summary>
/// Service for importing/parsing translation files from GitHub.
/// Inverse of FileExportService - parses file content into entries.
/// </summary>
public interface IFileImportService
{
    /// <summary>
    /// Parse a single file's content into entries.
    /// </summary>
    /// <param name="format">File format (resx, json, i18next, android, ios)</param>
    /// <param name="filePath">Path of the file (used for language detection)</param>
    /// <param name="content">File content as string</param>
    /// <param name="defaultLanguage">Project default language</param>
    /// <returns>Parsed entries with computed hashes</returns>
    ParsedResourceFile ParseFile(
        string format,
        string filePath,
        string content,
        string defaultLanguage);

    /// <summary>
    /// Parse multiple files into a unified entry dictionary.
    /// </summary>
    /// <param name="format">File format</param>
    /// <param name="files">Dictionary of filePath -> content</param>
    /// <param name="defaultLanguage">Project default language</param>
    /// <returns>Result containing entries and any parse errors</returns>
    ParseFilesResult ParseFiles(
        string format,
        Dictionary<string, string> files,
        string defaultLanguage);

    /// <summary>
    /// Detect language code from a file path based on format conventions.
    /// </summary>
    /// <param name="format">File format</param>
    /// <param name="filePath">File path</param>
    /// <param name="defaultLanguage">Project default language</param>
    /// <returns>Tuple of (languageCode, isDefault)</returns>
    (string languageCode, bool isDefault) DetectLanguageFromPath(
        string format,
        string filePath,
        string defaultLanguage);
}
