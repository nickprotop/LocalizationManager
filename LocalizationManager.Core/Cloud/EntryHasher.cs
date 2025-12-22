// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Security.Cryptography;
using System.Text;

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Provides consistent SHA256 hashing for translation entries.
/// Used for change detection in key-level sync.
/// </summary>
public static class EntryHasher
{
    /// <summary>
    /// Computes a SHA256 hash for a translation entry.
    /// Hash includes value and comment, with Unicode normalization (NFC).
    /// </summary>
    /// <param name="value">The translation value (required)</param>
    /// <param name="comment">Optional comment for the entry</param>
    /// <returns>Lowercase hexadecimal SHA256 hash (64 characters)</returns>
    public static string ComputeHash(string value, string? comment = null)
    {
        // Normalize to NFC (Unicode Canonical Decomposition, then Composition)
        var normalizedValue = value.Normalize(NormalizationForm.FormC);
        var normalizedComment = comment?.Normalize(NormalizationForm.FormC);

        var sb = new StringBuilder();
        sb.Append(normalizedValue);

        // Separator between value and comment (null byte)
        sb.Append('\0');
        sb.Append(normalizedComment ?? "");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes a SHA256 hash for a plural translation entry.
    /// Hash includes all plural forms sorted alphabetically by category.
    /// </summary>
    /// <param name="pluralForms">Dictionary of plural category to value (e.g., "one" -> "1 item")</param>
    /// <param name="comment">Optional comment for the entry</param>
    /// <returns>Lowercase hexadecimal SHA256 hash (64 characters)</returns>
    public static string ComputePluralHash(Dictionary<string, string> pluralForms, string? comment = null)
    {
        if (pluralForms == null || pluralForms.Count == 0)
        {
            return ComputeHash("", comment);
        }

        var sb = new StringBuilder();

        // Sort plural categories alphabetically for consistent hashing
        foreach (var kvp in pluralForms.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            var category = kvp.Key;
            var form = kvp.Value.Normalize(NormalizationForm.FormC);

            sb.Append(category);
            sb.Append('=');
            sb.Append(form);
            sb.Append('|');
        }

        // Separator between value and comment (null byte)
        sb.Append('\0');

        var normalizedComment = comment?.Normalize(NormalizationForm.FormC);
        sb.Append(normalizedComment ?? "");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes a SHA256 hash for a configuration property value.
    /// Uses deterministic JSON serialization for complex values.
    /// </summary>
    /// <param name="value">The property value as a string representation</param>
    /// <returns>Lowercase hexadecimal SHA256 hash (64 characters)</returns>
    public static string ComputeConfigHash(string value)
    {
        var normalizedValue = value.Normalize(NormalizationForm.FormC);
        var bytes = Encoding.UTF8.GetBytes(normalizedValue);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Computes a SHA256 hash for a resource key (key name + comment + isPlural).
    /// Used for detecting key-level changes.
    /// </summary>
    /// <param name="keyName">The resource key name</param>
    /// <param name="comment">Optional comment for the key</param>
    /// <param name="isPlural">Whether the key represents plural forms</param>
    /// <returns>Lowercase hexadecimal SHA256 hash (64 characters)</returns>
    public static string ComputeKeyHash(string keyName, string? comment = null, bool isPlural = false)
    {
        var normalizedKeyName = keyName.Normalize(NormalizationForm.FormC);
        var normalizedComment = comment?.Normalize(NormalizationForm.FormC);

        var sb = new StringBuilder();
        sb.Append(normalizedKeyName);
        sb.Append('\0');
        sb.Append(normalizedComment ?? "");
        sb.Append('\0');
        sb.Append(isPlural ? "1" : "0");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
