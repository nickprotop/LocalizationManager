// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.RegularExpressions;

namespace LocalizationManager.Core.Scanning.Scanners;

/// <summary>
/// A localization key referenced from a .NET Data Annotation attribute, paired with
/// the resource class named by the attribute's <c>ResourceType</c>.
/// </summary>
public sealed class DataAnnotationKeyMatch
{
    /// <summary>The localization key (e.g. value of <c>Name</c> / <c>ErrorMessageResourceName</c>).</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>
    /// Simple (non-namespace-qualified) name of the resource class from
    /// <c>typeof(...)</c>, e.g. <c>GlassResources</c>. Maps to a resx group BaseName.
    /// </summary>
    public string ResourceTypeClassName { get; init; } = string.Empty;

    /// <summary>1-based line number where the key literal appears.</summary>
    public int Line { get; init; }

    /// <summary>The full attribute text that matched (for context display).</summary>
    public string Pattern { get; init; } = string.Empty;
}

/// <summary>
/// Extracts localization keys from .NET Data Annotation attributes that pair a key
/// name with a resource type, e.g.
/// <c>[Display(Name = "K", ResourceType = typeof(R))]</c> and
/// <c>[Required(ErrorMessageResourceName = "K", ErrorMessageResourceType = typeof(R))]</c>.
///
/// A key is only recognized when an accompanying <c>ResourceType</c> /
/// <c>ErrorMessageResourceType</c> is present in the same attribute — a bare
/// <c>Name = "..."</c> (or <c>[DisplayName("...")]</c>) is a literal label, not a
/// localization key, so it is ignored.
/// </summary>
public static partial class DataAnnotationExtractor
{
    // Matches a single attribute: [AttrName( ... )] capturing the argument list.
    // Allows nested parens one level deep (enough for typeof(...) inside the args).
    [GeneratedRegex(@"\[\s*\w+\s*\(((?:[^()]|\([^()]*\))*)\)\s*\]", RegexOptions.Singleline)]
    private static partial Regex AttributeRegex();

    // Name-style key:   Name = "Key"   or   ErrorMessageResourceName = "Key"
    [GeneratedRegex(@"(?:ErrorMessageResourceName|Name)\s*=\s*""([^""]+)""")]
    private static partial Regex KeyNameRegex();

    // Resource type:    ResourceType = typeof(Foo.Bar.Class)   (Name or ErrorMessage variant)
    [GeneratedRegex(@"(?:ErrorMessageResourceType|ResourceType)\s*=\s*typeof\(\s*([\w.]+)\s*\)")]
    private static partial Regex ResourceTypeRegex();

    /// <summary>
    /// Returns every (key, resource-type) pair found in <paramref name="content"/>.
    /// Both a key literal and a resource type must be present in the same attribute
    /// for it to be reported.
    /// </summary>
    public static IReadOnlyList<DataAnnotationKeyMatch> Extract(string content)
    {
        if (string.IsNullOrEmpty(content))
            return Array.Empty<DataAnnotationKeyMatch>();

        var results = new List<DataAnnotationKeyMatch>();

        foreach (Match attr in AttributeRegex().Matches(content))
        {
            var args = attr.Groups[1].Value;

            var keyMatch = KeyNameRegex().Match(args);
            var typeMatch = ResourceTypeRegex().Match(args);

            // Require both: a bare Name="literal" without a ResourceType is not a key.
            if (!keyMatch.Success || !typeMatch.Success)
                continue;

            var key = keyMatch.Groups[1].Value;
            // typeof(My.App.GlassResources) -> GlassResources (simple class name maps to BaseName).
            var fullType = typeMatch.Groups[1].Value;
            var simpleType = fullType.Contains('.')
                ? fullType[(fullType.LastIndexOf('.') + 1)..]
                : fullType;

            results.Add(new DataAnnotationKeyMatch
            {
                Key = key,
                ResourceTypeClassName = simpleType,
                Line = LineOf(content, attr.Index),
                Pattern = attr.Value.Trim()
            });
        }

        return results;
    }

    private static int LineOf(string content, int index)
    {
        var line = 1;
        for (var i = 0; i < index && i < content.Length; i++)
        {
            if (content[i] == '\n') line++;
        }
        return line;
    }
}
