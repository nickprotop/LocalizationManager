using System.Text.RegularExpressions;
using LocalizationManager.Core.Scanning.Models;

namespace LocalizationManager.Core.Scanning.Scanners;

/// <summary>
/// Pattern matcher for C# source files
/// </summary>
public class CSharpScanner : PatternMatcher
{
    public override string[] SupportedExtensions => new[] { ".cs" };
    public override string LanguageName => "C#";

    // Default resource class names (used if not configured)
    private static readonly string[] DefaultResourceClassNames = { "Resources", "Strings", "AppResources" };

    // Default localization method names (used if not configured)
    private static readonly string[] DefaultLocalizationMethods = { "GetString", "GetLocalizedString", "Translate", "L", "T" };

    // Pattern: Indexer access like _localizer["KeyName"]
    private static readonly Regex IndexerPattern = new(
        @"(\w+)\s*\[\s*""([^""]+)""\s*\]",
        RegexOptions.Compiled);

    public override List<KeyReference> ScanFile(
        string filePath,
        bool strictMode = false,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null)
    {
        var originalContent = ReadFileContent(filePath);
        return ScanContent(filePath, originalContent, strictMode, resourceClassNames, localizationMethods);
    }

    public override List<KeyReference> ScanContent(
        string filePath,
        string content,
        bool strictMode = false,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null)
    {
        var references = new List<KeyReference>();

        if (string.IsNullOrEmpty(content))
            return references;

        var originalContent = content;

        // Remove comments to avoid false positives
        var cleanedContent = RemoveComments(originalContent);

        // Use provided configuration or defaults
        var classNames = resourceClassNames ?? DefaultResourceClassNames.ToList();
        var methodNames = localizationMethods ?? DefaultLocalizationMethods.ToList();

        // Scan for property access patterns (e.g., Resources.SaveButton)
        ScanPropertyAccess(cleanedContent, originalContent, filePath, references, classNames);

        // Scan for GetString patterns
        ScanGetStringCalls(cleanedContent, originalContent, filePath, references, methodNames);

        // Scan for indexer patterns
        ScanIndexerAccess(cleanedContent, originalContent, filePath, references);

        // Scan for dynamic patterns (unless strict mode)
        if (!strictMode)
        {
            ScanDynamicPatterns(cleanedContent, originalContent, filePath, references, methodNames);
        }

        return references;
    }

    /// <summary>
    /// Removes single-line (//) and multi-line (/* */) comments from C# code
    /// while preserving string literals
    /// </summary>
    private string RemoveComments(string content)
    {
        // This regex matches:
        // 1. String literals (both @"" and regular "") - we keep these
        // 2. Single-line comments // ... (we remove these)
        // 3. Multi-line comments /* ... */ (we remove these)
        var pattern = @"(""(?:[^""\\]|\\.)*""|@""(?:""""|[^""])*"")|//.*?$|/\*.*?\*/";

        return Regex.Replace(content, pattern, match =>
        {
            // If it's a string literal (group 1), keep it
            if (!string.IsNullOrEmpty(match.Groups[1].Value))
                return match.Groups[1].Value;

            // Otherwise it's a comment, replace with spaces to preserve line numbers
            return new string(' ', match.Value.Length);
        }, RegexOptions.Multiline | RegexOptions.Singleline);
    }

    private void ScanPropertyAccess(string content, string originalContent, string filePath, List<KeyReference> references, List<string> classNames)
    {
        // Build dynamic regex: (Resources|Strings|AppResources)\.(\w+)\b(?!\s*\()
        // This matches: ClassName.PropertyName but NOT ClassName.MethodName()
        // \b after \w+ ensures we match the full word, then (?!\s*\() rejects if followed by (
        var escapedNames = classNames.Select(Regex.Escape);
        var pattern = $@"({string.Join("|", escapedNames)})\.(\w+)\b(?!\s*\()";
        var regex = new Regex(pattern, RegexOptions.Compiled);

        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            var className = match.Groups[1].Value;
            var propertyName = match.Groups[2].Value;

            references.Add(new KeyReference
            {
                Key = propertyName,
                FilePath = filePath,
                Line = GetLineNumber(originalContent, match.Index),
                Pattern = match.Value,
                Context = GetContext(originalContent, match.Index),
                Confidence = ConfidenceLevel.High
            });
        }
    }

    private void ScanGetStringCalls(string content, string originalContent, string filePath, List<KeyReference> references, List<string> methodNames)
    {
        // Build dynamic regex: \b(GetString|Translate|L|T)\s*\(\s*"([^"]+)"\s*\)
        // \b ensures word boundary - prevents matching "CreateClient" as "t"
        var escapedMethods = methodNames.Select(Regex.Escape);
        var pattern = $@"\b({string.Join("|", escapedMethods)})\s*\(\s*""([^""]+)""\s*\)";
        var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            var keyName = match.Groups[2].Value;  // Group 2 is the key string

            references.Add(new KeyReference
            {
                Key = keyName,
                FilePath = filePath,
                Line = GetLineNumber(originalContent, match.Index),
                Pattern = match.Value,
                Context = GetContext(originalContent, match.Index),
                Confidence = ConfidenceLevel.High
            });
        }
    }

    private void ScanIndexerAccess(string content, string originalContent, string filePath, List<KeyReference> references)
    {
        var matches = IndexerPattern.Matches(content);

        foreach (Match match in matches)
        {
            var variableName = match.Groups[1].Value;
            var keyName = match.Groups[2].Value;

            // Check if variable name suggests localization
            if (IsLikelyLocalizerVariable(variableName))
            {
                references.Add(new KeyReference
                {
                    Key = keyName,
                    FilePath = filePath,
                    Line = GetLineNumber(originalContent, match.Index),
                    Pattern = match.Value,
                    Context = GetContext(originalContent, match.Index),
                    Confidence = ConfidenceLevel.High
                });
            }
        }
    }

    private void ScanDynamicPatterns(string content, string originalContent, string filePath, List<KeyReference> references, List<string> methodNames)
    {
        var escapedMethods = methodNames.Select(Regex.Escape);

        // Pattern: matches both Method($"...") and Class.Method($"...")
        // The optional (\w+\.)? matches the class prefix if present
        var methodPattern = $@"(?:\w+\.)?({string.Join("|", escapedMethods)})\s*\(\s*\$""[^""]*\{{[^}}]+\}}[^""]*""\s*\)";
        var methodRegex = new Regex(methodPattern, RegexOptions.Compiled);

        var matches = methodRegex.Matches(content);

        foreach (Match match in matches)
        {
            references.Add(new KeyReference
            {
                Key = "<dynamic>",
                FilePath = filePath,
                Line = GetLineNumber(originalContent, match.Index),
                Pattern = match.Value,
                Context = GetContext(originalContent, match.Index),
                Confidence = ConfidenceLevel.Low,
                Warning = "Dynamic key reference detected - cannot determine exact key name"
            });
        }

        // Pattern for indexer with interpolated strings: variable[$"..."]
        var indexerPattern = @"(\w+)\s*\[\s*\$""[^""]*\{[^}]+\}[^""]*""\s*\]";
        var indexerRegex = new Regex(indexerPattern, RegexOptions.Compiled);

        var indexerMatches = indexerRegex.Matches(content);

        foreach (Match match in indexerMatches)
        {
            var variableName = match.Groups[1].Value;

            // Check if variable name suggests localization
            if (IsLikelyLocalizerVariable(variableName))
            {
                references.Add(new KeyReference
                {
                    Key = "<dynamic>",
                    FilePath = filePath,
                    Line = GetLineNumber(originalContent, match.Index),
                    Pattern = match.Value,
                    Context = GetContext(originalContent, match.Index),
                    Confidence = ConfidenceLevel.Low,
                    Warning = "Dynamic key reference detected - cannot determine exact key name"
                });
            }
        }
    }

    private bool IsLikelyLocalizerVariable(string variableName)
    {
        var lowerName = variableName.ToLowerInvariant();
        return lowerName.Contains("localiz") ||
               lowerName.Contains("resource") ||
               lowerName.Contains("strings") ||
               lowerName.Contains("lang") ||
               lowerName.Contains("i18n") ||
               lowerName.Contains("l10n") ||
               lowerName.StartsWith("_l") ||
               lowerName.StartsWith("_localizer");
    }
}
