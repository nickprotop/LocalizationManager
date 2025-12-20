// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text.RegularExpressions;

namespace LocalizationManager.Core.Backends.Android;

/// <summary>
/// Maps between Android folder naming conventions and .NET culture codes.
/// Android uses: values, values-es, values-zh-rCN, values-b+sr+Latn
/// .NET uses: "", "es", "zh-CN", "sr-Latn"
/// </summary>
public static partial class AndroidCultureMapper
{
    /// <summary>
    /// Converts Android folder name to .NET culture code.
    /// Examples:
    ///   "values" -> "" (default)
    ///   "values-es" -> "es"
    ///   "values-zh-rCN" -> "zh-CN"
    ///   "values-b+sr+Latn" -> "sr-Latn" (BCP 47 format)
    /// </summary>
    public static string FolderToCode(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
            throw new ArgumentException("Folder name cannot be null or empty", nameof(folderName));

        // Default language folder
        if (folderName.Equals("values", StringComparison.OrdinalIgnoreCase))
            return "";

        if (!folderName.StartsWith("values-", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid Android resource folder: {folderName}", nameof(folderName));

        var suffix = folderName.Substring(7); // Remove "values-"

        // Handle BCP 47 format (values-b+sr+Latn)
        if (suffix.StartsWith("b+", StringComparison.OrdinalIgnoreCase))
        {
            return suffix.Substring(2).Replace("+", "-");
        }

        // Handle region format (values-zh-rCN -> zh-CN)
        // Pattern: ll-rRR where ll=language, RR=region
        var regionMatch = RegionPattern().Match(suffix);
        if (regionMatch.Success)
        {
            return $"{regionMatch.Groups[1].Value}-{regionMatch.Groups[2].Value}";
        }

        // Simple language code (es, fr, de)
        return suffix;
    }

    /// <summary>
    /// Converts .NET culture code to Android folder name.
    /// Examples:
    ///   "" -> "values" (default)
    ///   "es" -> "values-es"
    ///   "zh-CN" -> "values-zh-rCN"
    ///   "sr-Latn" -> "values-b+sr+Latn"
    /// </summary>
    public static string CodeToFolder(string cultureCode)
    {
        if (string.IsNullOrEmpty(cultureCode))
            return "values";

        // Handle region codes (zh-CN -> values-zh-rCN)
        if (cultureCode.Contains('-'))
        {
            var parts = cultureCode.Split('-');
            if (parts.Length == 2)
            {
                // Check if it's a simple region code (2 uppercase letters)
                if (parts[1].Length == 2 && parts[1].All(char.IsLetter))
                {
                    return $"values-{parts[0]}-r{parts[1].ToUpperInvariant()}";
                }
                // Complex scripts (sr-Latn -> values-b+sr+Latn)
                return $"values-b+{cultureCode.Replace("-", "+")}";
            }
            // Multiple parts, use BCP 47 format
            return $"values-b+{cultureCode.Replace("-", "+")}";
        }

        // Simple language code
        return $"values-{cultureCode}";
    }

    /// <summary>
    /// Checks if a folder name is a valid Android resource folder.
    /// </summary>
    public static bool IsValidResourceFolder(string folderName)
    {
        if (string.IsNullOrEmpty(folderName))
            return false;

        // Must be "values" or start with "values-"
        return folderName.Equals("values", StringComparison.OrdinalIgnoreCase) ||
               folderName.StartsWith("values-", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^([a-z]{2,3})-r([A-Z]{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex RegionPattern();
}
