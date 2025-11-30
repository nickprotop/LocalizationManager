using System.Text.RegularExpressions;
using LocalizationManager.Core.Scanning.Models;

namespace LocalizationManager.Core.Scanning.Scanners;

/// <summary>
/// Pattern matcher for Razor view files (.cshtml, .razor)
/// </summary>
public class RazorScanner : PatternMatcher
{
    public override string[] SupportedExtensions => new[] { ".cshtml", ".razor" };
    public override string LanguageName => "Razor";

    // Default resource class names (used if not configured)
    private static readonly string[] DefaultResourceClassNames = { "Resources", "Strings", "AppResources" };

    // Default localization method names (used if not configured)
    private static readonly string[] DefaultLocalizationMethods = { "GetString", "GetLocalizedString", "Translate", "L", "T" };

    // Pattern: @Localizer["KeyName"]
    private static readonly Regex LocalizerIndexerPattern = new(
        @"@(\w+)\s*\[\s*""([^""]+)""\s*\]",
        RegexOptions.Compiled);

    // Pattern: @IHtmlLocalizer["KeyName"] or @IStringLocalizer["KeyName"]
    private static readonly Regex LocalizerTypePattern = new(
        @"@I(?:Html|String)Localizer\s*\[\s*""([^""]+)""\s*\]",
        RegexOptions.Compiled);

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
        var methodNames = localizationMethods ?? DefaultLocalizationMethods.ToList();

        // Scan for @Resources.KeyName patterns
        ScanResourceProperties(content, filePath, references, classNames);

        // Scan for @Localizer["KeyName"] patterns
        ScanLocalizerIndexers(content, filePath, references);

        // Scan for @IHtmlLocalizer["KeyName"] patterns
        ScanLocalizerTypes(content, filePath, references);

        // Scan for code blocks
        if (!strictMode)
        {
            ScanCodeBlocks(content, filePath, references, classNames, methodNames);
        }

        return references;
    }

    private void ScanResourceProperties(string content, string filePath, List<KeyReference> references, List<string> classNames)
    {
        // Build dynamic regex: @(Resources|Strings|AppResources)\.(\w+)\b(?!\s*\()
        // This matches: @ClassName.PropertyName but NOT @ClassName.MethodName()
        var escapedNames = classNames.Select(Regex.Escape);
        var pattern = $@"@({string.Join("|", escapedNames)})\.(\w+)\b(?!\s*\()";
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
                Line = GetLineNumber(content, match.Index),
                Pattern = match.Value,
                Context = GetContext(content, match.Index),
                Confidence = ConfidenceLevel.High
            });
        }
    }

    private void ScanLocalizerIndexers(string content, string filePath, List<KeyReference> references)
    {
        var matches = LocalizerIndexerPattern.Matches(content);

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
                    Line = GetLineNumber(content, match.Index),
                    Pattern = match.Value,
                    Context = GetContext(content, match.Index),
                    Confidence = ConfidenceLevel.High
                });
            }
        }
    }

    private void ScanLocalizerTypes(string content, string filePath, List<KeyReference> references)
    {
        var matches = LocalizerTypePattern.Matches(content);

        foreach (Match match in matches)
        {
            var keyName = match.Groups[1].Value;

            references.Add(new KeyReference
            {
                Key = keyName,
                FilePath = filePath,
                Line = GetLineNumber(content, match.Index),
                Pattern = match.Value,
                Context = GetContext(content, match.Index),
                Confidence = ConfidenceLevel.High
            });
        }
    }

    private void ScanCodeBlocks(string content, string filePath, List<KeyReference> references, List<string> classNames, List<string> methodNames)
    {
        // Scan @{...} blocks
        ScanSimpleCodeBlocks(content, filePath, references, classNames, methodNames);

        // Scan @code {...} blocks
        ScanNamedCodeBlocks(content, filePath, references, classNames, methodNames, "code");

        // Scan @functions {...} blocks
        ScanNamedCodeBlocks(content, filePath, references, classNames, methodNames, "functions");

        // Scan for dynamic keys in code blocks
        ScanDynamicCodeBlockKeys(content, filePath, references, methodNames);
    }

    private void ScanSimpleCodeBlocks(string content, string filePath, List<KeyReference> references, List<string> classNames, List<string> methodNames)
    {
        // Match @{...} blocks (simple code blocks)
        // Use the same approach as ScanNamedCodeBlocks for consistency
        var blockPattern = @"@\{(?:[^{}]|(?<open>\{)|(?<-open>\}))*?(?(open)(?!))\}";
        var blockRegex = new Regex(blockPattern, RegexOptions.Compiled | RegexOptions.Singleline);

        var blockMatches = blockRegex.Matches(content);

        foreach (Match blockMatch in blockMatches)
        {
            var blockContent = blockMatch.Value;
            var blockStartIndex = blockMatch.Index;

            // Scan for property access: Resources.KeyName
            var escapedClassNames = classNames.Select(Regex.Escape);
            var propertyPattern = $@"({string.Join("|", escapedClassNames)})\.(\w+)\b(?!\s*\()";
            var propertyRegex = new Regex(propertyPattern, RegexOptions.Compiled);

            foreach (Match propMatch in propertyRegex.Matches(blockContent))
            {
                var keyName = propMatch.Groups[2].Value;
                var absoluteIndex = blockStartIndex + propMatch.Index;

                references.Add(new KeyReference
                {
                    Key = keyName,
                    FilePath = filePath,
                    Line = GetLineNumber(content, absoluteIndex),
                    Pattern = propMatch.Value,
                    Context = GetContext(content, absoluteIndex),
                    Confidence = ConfidenceLevel.High
                });
            }

            // Scan for method calls: GetString("KeyName")
            var escapedMethodNames = methodNames.Select(Regex.Escape);
            var methodPattern = $@"\b({string.Join("|", escapedMethodNames)})\(""([^""]+)""\)";
            var methodRegex = new Regex(methodPattern, RegexOptions.Compiled);

            foreach (Match methodMatch in methodRegex.Matches(blockContent))
            {
                var keyName = methodMatch.Groups[2].Value;
                var absoluteIndex = blockStartIndex + methodMatch.Index;

                references.Add(new KeyReference
                {
                    Key = keyName,
                    FilePath = filePath,
                    Line = GetLineNumber(content, absoluteIndex),
                    Pattern = methodMatch.Value,
                    Context = GetContext(content, absoluteIndex),
                    Confidence = ConfidenceLevel.High
                });
            }

            // Scan for indexer access: Localizer["KeyName"]
            var indexerPattern = @"(\w+)\s*\[\s*""([^""]+)""\s*\]";
            var indexerRegex = new Regex(indexerPattern, RegexOptions.Compiled);

            foreach (Match indexerMatch in indexerRegex.Matches(blockContent))
            {
                var variableName = indexerMatch.Groups[1].Value;
                var keyName = indexerMatch.Groups[2].Value;
                var absoluteIndex = blockStartIndex + indexerMatch.Index;

                if (IsLikelyLocalizerVariable(variableName))
                {
                    references.Add(new KeyReference
                    {
                        Key = keyName,
                        FilePath = filePath,
                        Line = GetLineNumber(content, absoluteIndex),
                        Pattern = indexerMatch.Value,
                        Context = GetContext(content, absoluteIndex),
                        Confidence = ConfidenceLevel.High
                    });
                }
            }
        }
    }

    private void ScanNamedCodeBlocks(string content, string filePath, List<KeyReference> references, List<string> classNames, List<string> methodNames, string blockName)
    {
        // Match @code {...} or @functions {...} blocks
        // This pattern extracts the entire block content, then we scan within it
        var blockPattern = $@"@{blockName}\s*\{{(?:[^{{}}]|(?<open>\{{)|(?<-open>\}}))*?(?(open)(?!))\}}";
        var blockRegex = new Regex(blockPattern, RegexOptions.Compiled | RegexOptions.Singleline);

        var blockMatches = blockRegex.Matches(content);

        foreach (Match blockMatch in blockMatches)
        {
            var blockContent = blockMatch.Value;
            var blockStartIndex = blockMatch.Index;

            // Scan for property access: Resources.KeyName
            var escapedClassNames = classNames.Select(Regex.Escape);
            var propertyPattern = $@"({string.Join("|", escapedClassNames)})\.(\w+)\b(?!\s*\()";
            var propertyRegex = new Regex(propertyPattern, RegexOptions.Compiled);

            foreach (Match propMatch in propertyRegex.Matches(blockContent))
            {
                var keyName = propMatch.Groups[2].Value;
                var absoluteIndex = blockStartIndex + propMatch.Index;

                references.Add(new KeyReference
                {
                    Key = keyName,
                    FilePath = filePath,
                    Line = GetLineNumber(content, absoluteIndex),
                    Pattern = propMatch.Value,
                    Context = GetContext(content, absoluteIndex),
                    Confidence = ConfidenceLevel.High
                });
            }

            // Scan for method calls: GetString("KeyName")
            var escapedMethodNames = methodNames.Select(Regex.Escape);
            var methodPattern = $@"\b({string.Join("|", escapedMethodNames)})\(""([^""]+)""\)";
            var methodRegex = new Regex(methodPattern, RegexOptions.Compiled);

            foreach (Match methodMatch in methodRegex.Matches(blockContent))
            {
                var keyName = methodMatch.Groups[2].Value;
                var absoluteIndex = blockStartIndex + methodMatch.Index;

                references.Add(new KeyReference
                {
                    Key = keyName,
                    FilePath = filePath,
                    Line = GetLineNumber(content, absoluteIndex),
                    Pattern = methodMatch.Value,
                    Context = GetContext(content, absoluteIndex),
                    Confidence = ConfidenceLevel.High
                });
            }

            // Scan for indexer access: Localizer["KeyName"]
            var indexerPattern = @"(\w+)\s*\[\s*""([^""]+)""\s*\]";
            var indexerRegex = new Regex(indexerPattern, RegexOptions.Compiled);

            foreach (Match indexerMatch in indexerRegex.Matches(blockContent))
            {
                var variableName = indexerMatch.Groups[1].Value;
                var keyName = indexerMatch.Groups[2].Value;
                var absoluteIndex = blockStartIndex + indexerMatch.Index;

                if (IsLikelyLocalizerVariable(variableName))
                {
                    references.Add(new KeyReference
                    {
                        Key = keyName,
                        FilePath = filePath,
                        Line = GetLineNumber(content, absoluteIndex),
                        Pattern = indexerMatch.Value,
                        Context = GetContext(content, absoluteIndex),
                        Confidence = ConfidenceLevel.High
                    });
                }
            }
        }
    }

    private void ScanDynamicCodeBlockKeys(string content, string filePath, List<KeyReference> references, List<string> methodNames)
    {
        // Scan for dynamic keys in @{...}, @code {...}, and @functions {...} blocks
        // Match method calls with string interpolation: GetString($"...")
        var blockPatterns = new[]
        {
            @"@\{(?:[^{}]|(?<open>\{)|(?<-open>\}))*?(?(open)(?!))\}",            // @{...} with nested braces
            @"@code\s*\{(?:[^{}]|(?<open>\{)|(?<-open>\}))*?(?(open)(?!))\}",     // @code {...}
            @"@functions\s*\{(?:[^{}]|(?<open>\{)|(?<-open>\}))*?(?(open)(?!))\}" // @functions {...}
        };

        foreach (var blockPattern in blockPatterns)
        {
            var blockRegex = new Regex(blockPattern, RegexOptions.Compiled | RegexOptions.Singleline);
            var blockMatches = blockRegex.Matches(content);

            foreach (Match blockMatch in blockMatches)
            {
                var blockContent = blockMatch.Value;
                var blockStartIndex = blockMatch.Index;

                // Pattern for method calls with interpolated strings: Method($"...")
                var escapedMethodNames = methodNames.Select(Regex.Escape);
                var dynamicMethodPattern = $@"\b({string.Join("|", escapedMethodNames)})\(\s*\$""[^""]*\{{[^}}]+\}}[^""]*""\s*\)";
                var dynamicMethodRegex = new Regex(dynamicMethodPattern, RegexOptions.Compiled);

                foreach (Match dynamicMatch in dynamicMethodRegex.Matches(blockContent))
                {
                    var absoluteIndex = blockStartIndex + dynamicMatch.Index;

                    references.Add(new KeyReference
                    {
                        Key = "<dynamic>",
                        FilePath = filePath,
                        Line = GetLineNumber(content, absoluteIndex),
                        Pattern = dynamicMatch.Value,
                        Context = GetContext(content, absoluteIndex),
                        Confidence = ConfidenceLevel.Low,
                        Warning = "Dynamic key reference detected - cannot determine exact key name"
                    });
                }

                // Pattern for indexer with interpolated strings: variable[$"..."]
                var dynamicIndexerPattern = @"(\w+)\s*\[\s*\$""[^""]*\{[^}]+\}[^""]*""\s*\]";
                var dynamicIndexerRegex = new Regex(dynamicIndexerPattern, RegexOptions.Compiled);

                foreach (Match dynamicMatch in dynamicIndexerRegex.Matches(blockContent))
                {
                    var variableName = dynamicMatch.Groups[1].Value;
                    var absoluteIndex = blockStartIndex + dynamicMatch.Index;

                    if (IsLikelyLocalizerVariable(variableName))
                    {
                        references.Add(new KeyReference
                        {
                            Key = "<dynamic>",
                            FilePath = filePath,
                            Line = GetLineNumber(content, absoluteIndex),
                            Pattern = dynamicMatch.Value,
                            Context = GetContext(content, absoluteIndex),
                            Confidence = ConfidenceLevel.Low,
                            Warning = "Dynamic key reference detected - cannot determine exact key name"
                        });
                    }
                }
            }
        }
    }

    private bool IsLikelyLocalizerVariable(string variableName)
    {
        var lowerName = variableName.ToLowerInvariant();
        return lowerName.Contains("localiz") ||
               lowerName.Contains("resource") ||
               lowerName.Contains("strings") ||
               lowerName.Equals("l") ||
               lowerName.Equals("loc");
    }
}
