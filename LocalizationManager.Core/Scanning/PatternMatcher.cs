using LocalizationManager.Core.Scanning.Models;

namespace LocalizationManager.Core.Scanning;

/// <summary>
/// Base class for language-specific pattern matchers
/// </summary>
public abstract class PatternMatcher
{
    /// <summary>
    /// Scan a file for localization key references
    /// </summary>
    /// <param name="filePath">Path to the file to scan</param>
    /// <param name="strictMode">If true, only match high-confidence static references</param>
    /// <param name="resourceClassNames">Optional list of resource class names to detect</param>
    /// <param name="localizationMethods">Optional list of localization method names to detect</param>
    /// <returns>List of key references found in the file</returns>
    public abstract List<KeyReference> ScanFile(
        string filePath,
        bool strictMode = false,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null);

    /// <summary>
    /// Scan file content (string) for localization key references
    /// </summary>
    /// <param name="filePath">Path to the file (used for extension detection and KeyReference.FilePath)</param>
    /// <param name="content">The file content to scan</param>
    /// <param name="strictMode">If true, only match high-confidence static references</param>
    /// <param name="resourceClassNames">Optional list of resource class names to detect</param>
    /// <param name="localizationMethods">Optional list of localization method names to detect</param>
    /// <returns>List of key references found in the content</returns>
    public abstract List<KeyReference> ScanContent(
        string filePath,
        string content,
        bool strictMode = false,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null);

    /// <summary>
    /// Get file extensions supported by this pattern matcher
    /// </summary>
    public abstract string[] SupportedExtensions { get; }

    /// <summary>
    /// Name of the language/framework this matcher supports
    /// </summary>
    public abstract string LanguageName { get; }

    /// <summary>
    /// Read file content safely with error handling
    /// </summary>
    protected string ReadFileContent(string filePath)
    {
        try
        {
            return File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not read file {filePath}: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Get line number for a match position in the content
    /// </summary>
    protected int GetLineNumber(string content, int position)
    {
        if (position < 0 || position >= content.Length)
            return 1;

        var lineNumber = 1;
        for (int i = 0; i < position && i < content.Length; i++)
        {
            if (content[i] == '\n')
                lineNumber++;
        }

        return lineNumber;
    }

    /// <summary>
    /// Get context (surrounding code) for a match
    /// </summary>
    protected string GetContext(string content, int position, int contextLength = 80)
    {
        var start = Math.Max(0, position - contextLength / 2);
        var length = Math.Min(contextLength, content.Length - start);

        var context = content.Substring(start, length).Trim();

        // Replace newlines and excessive whitespace
        context = System.Text.RegularExpressions.Regex.Replace(context, @"\s+", " ");

        return context;
    }
}
