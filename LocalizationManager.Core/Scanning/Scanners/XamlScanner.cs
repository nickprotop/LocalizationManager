using System.Text.RegularExpressions;
using LocalizationManager.Core.Scanning.Models;

namespace LocalizationManager.Core.Scanning.Scanners;

/// <summary>
/// Pattern matcher for XAML files (WPF, UWP, MAUI)
/// </summary>
public class XamlScanner : PatternMatcher
{
    public override string[] SupportedExtensions => new[] { ".xaml" };
    public override string LanguageName => "XAML";

    // Default resource class names (used if not configured)
    private static readonly string[] DefaultResourceClassNames = { "Resources", "Strings", "AppResources" };

    // Pattern: {StaticResource KeyName} - for resource dictionaries
    private static readonly Regex StaticResourceKeyPattern = new(
        @"\{StaticResource\s+([^\}]+)\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Pattern: {DynamicResource KeyName}
    private static readonly Regex DynamicResourcePattern = new(
        @"\{DynamicResource\s+([^\}]+)\}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public override List<KeyReference> ScanFile(
        string filePath,
        bool strictMode = false,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null)
    {
        var content = ReadFileContent(filePath);
        return ScanContent(filePath, content, strictMode, resourceClassNames, localizationMethods);
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

        // Use provided configuration or defaults
        var classNames = resourceClassNames ?? DefaultResourceClassNames.ToList();

        // Scan for {x:Static res:Resources.KeyName} patterns
        ScanStaticResources(content, filePath, references, classNames);

        // Scan for binding patterns
        ScanBindingResources(content, filePath, references, classNames);

        // Scan for {StaticResource KeyName} patterns (lower confidence)
        if (!strictMode)
        {
            ScanStaticResourceKeys(content, filePath, references);
            ScanDynamicResourceKeys(content, filePath, references);
        }

        return references;
    }

    private void ScanStaticResources(string content, string filePath, List<KeyReference> references, List<string> classNames)
    {
        // Build dynamic regex: {x:Static (?:res:)?(Resources|Strings|AppResources)\.(\w+)}
        // But skip if it's part of a Binding Source pattern (handled by ScanBindingResources)
        var escapedNames = classNames.Select(Regex.Escape);
        var pattern = $@"\{{x:Static\s+(?:res:)?({string.Join("|", escapedNames)})\.(\w+)\}}";
        var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            // Skip if this is inside a Binding Source pattern
            // Check if there's "{Binding" before this match and no closing "}" between them
            var beforeMatch = content.Substring(Math.Max(0, match.Index - 50), Math.Min(50, match.Index));
            if (beforeMatch.Contains("{Binding") && beforeMatch.LastIndexOf('{') > beforeMatch.LastIndexOf('}'))
            {
                continue; // This will be handled by ScanBindingResources
            }

            var className = match.Groups[1].Value;
            var propertyName = match.Groups[2].Value;

            references.Add(new KeyReference
            {
                Key = propertyName,
                FilePath = filePath,
                Line = GetLineNumber(content, match.Index),
                Pattern = match.Value,
                Context = GetContext(content, match.Index, 100),
                Confidence = ConfidenceLevel.High
            });
        }
    }

    private void ScanBindingResources(string content, string filePath, List<KeyReference> references, List<string> classNames)
    {
        // Build dynamic regex: {Binding Source={x:Static (?:res:)?(Resources|Strings)\.(\w+)}}
        var escapedNames = classNames.Select(Regex.Escape);
        var pattern = $@"\{{Binding\s+Source=\{{x:Static\s+(?:res:)?({string.Join("|", escapedNames)})\.(\w+)\}}\}}";
        var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            var className = match.Groups[1].Value;
            var propertyName = match.Groups[2].Value;

            references.Add(new KeyReference
            {
                Key = propertyName,
                FilePath = filePath,
                Line = GetLineNumber(content, match.Index),
                Pattern = match.Value,
                Context = GetContext(content, match.Index, 100),
                Confidence = ConfidenceLevel.High
            });
        }
    }

    private void ScanStaticResourceKeys(string content, string filePath, List<KeyReference> references)
    {
        var matches = StaticResourceKeyPattern.Matches(content);

        foreach (Match match in matches)
        {
            var keyName = match.Groups[1].Value.Trim();

            // Skip if it looks like a system resource or style
            if (IsLikelySystemResource(keyName))
                continue;

            references.Add(new KeyReference
            {
                Key = keyName,
                FilePath = filePath,
                Line = GetLineNumber(content, match.Index),
                Pattern = match.Value,
                Context = GetContext(content, match.Index, 100),
                Confidence = ConfidenceLevel.Medium,
                Warning = "StaticResource reference - may be from resource dictionary rather than localization"
            });
        }
    }

    private void ScanDynamicResourceKeys(string content, string filePath, List<KeyReference> references)
    {
        var matches = DynamicResourcePattern.Matches(content);

        foreach (Match match in matches)
        {
            var keyName = match.Groups[1].Value.Trim();

            if (IsLikelySystemResource(keyName))
                continue;

            references.Add(new KeyReference
            {
                Key = keyName,
                FilePath = filePath,
                Line = GetLineNumber(content, match.Index),
                Pattern = match.Value,
                Context = GetContext(content, match.Index, 100),
                Confidence = ConfidenceLevel.Medium,
                Warning = "DynamicResource reference - may be from resource dictionary rather than localization"
            });
        }
    }

    private bool IsLikelySystemResource(string resourceKey)
    {
        var lowerKey = resourceKey.ToLowerInvariant();

        // Common WPF/XAML system resources to skip
        return lowerKey.Contains("brush") ||
               lowerKey.Contains("color") ||
               lowerKey.Contains("style") ||
               lowerKey.Contains("template") ||
               lowerKey.Contains("converter") ||
               lowerKey.Contains("animation");
    }
}
