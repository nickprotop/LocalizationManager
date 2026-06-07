using System.Text.RegularExpressions;

namespace LocalizationManager.Core.Scanning.Scanners;

/// <summary>
/// Extracts variable names that are declared as injected localizers in Razor
/// content, e.g. <c>@inject IStringLocalizer&lt;T&gt; VariableName</c> (also
/// IHtmlLocalizer / IStringLocalizerFactory). These names are then treated as
/// localizer indexers regardless of the configured method list, so that
/// <c>@Q["Key"]</c> resolves even when the variable name is project-specific.
/// </summary>
public static class InjectedLocalizerExtractor
{
    // @inject IStringLocalizer<Foo.Bar> VarName
    // @inject IHtmlLocalizer<Foo> VarName
    private static readonly Regex InjectPattern = new(
        @"@inject\s+I(?:String|Html)Localizer(?:<[^>]+>)?\s+(\w+)",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns the distinct injected-localizer variable names found in
    /// <paramref name="content"/>, in first-seen order.
    /// </summary>
    public static IReadOnlyList<string> Extract(string content)
    {
        if (string.IsNullOrEmpty(content))
            return Array.Empty<string>();

        var seen = new List<string>();
        foreach (Match m in InjectPattern.Matches(content))
        {
            var name = m.Groups[1].Value;
            if (!seen.Contains(name))
                seen.Add(name);
        }
        return seen;
    }
}
