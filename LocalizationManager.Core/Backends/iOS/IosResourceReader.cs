// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.iOS;

/// <summary>
/// Reads iOS .strings and .stringsdict files and parses them into ResourceFile objects.
/// Merges content from both file types for complete plural support.
/// </summary>
public class IosResourceReader : IResourceReader
{
    private readonly StringsFileParser _stringsParser = new();
    private readonly StringsdictParser _stringsdictParser = new();

    /// <inheritdoc />
    public ResourceFile Read(LanguageInfo language)
    {
        if (string.IsNullOrEmpty(language.FilePath))
            throw new ArgumentException("Language file path is required", nameof(language));

        var entries = new List<ResourceEntry>();

        // Read .strings file
        var stringsPath = language.FilePath;
        if (stringsPath.EndsWith(".stringsdict", StringComparison.OrdinalIgnoreCase))
        {
            stringsPath = Path.ChangeExtension(stringsPath, ".strings");
        }

        if (File.Exists(stringsPath))
        {
            var stringsContent = File.ReadAllText(stringsPath);
            var stringsEntries = _stringsParser.Parse(stringsContent);

            foreach (var entry in stringsEntries)
            {
                entries.Add(new ResourceEntry
                {
                    Key = entry.Key,
                    Value = entry.Value,
                    Comment = entry.Comment
                });
            }
        }

        // Read .stringsdict file for plurals
        var stringsdictPath = Path.ChangeExtension(stringsPath, ".stringsdict");
        if (File.Exists(stringsdictPath))
        {
            var stringsdictContent = File.ReadAllText(stringsdictPath);
            var pluralEntries = _stringsdictParser.Parse(stringsdictContent);

            foreach (var entry in pluralEntries)
            {
                // Check if there's already an entry with this key (from .strings)
                var existingIndex = entries.FindIndex(e =>
                    e.Key.Equals(entry.Key, StringComparison.Ordinal));

                var defaultValue = entry.PluralForms.GetValueOrDefault("other") ??
                                  entry.PluralForms.Values.FirstOrDefault() ?? "";

                var pluralEntry = new ResourceEntry
                {
                    Key = entry.Key,
                    Value = defaultValue,
                    Comment = null,
                    IsPlural = true,
                    PluralForms = entry.PluralForms
                };

                if (existingIndex >= 0)
                {
                    // Replace with plural version, preserving comment
                    pluralEntry.Comment = entries[existingIndex].Comment;
                    entries[existingIndex] = pluralEntry;
                }
                else
                {
                    entries.Add(pluralEntry);
                }
            }
        }

        return new ResourceFile { Language = language, Entries = entries };
    }

    /// <inheritdoc />
    public Task<ResourceFile> ReadAsync(LanguageInfo language, CancellationToken ct = default)
        => Task.FromResult(Read(language));

    /// <inheritdoc />
    public ResourceFile Read(TextReader reader, LanguageInfo metadata)
    {
        // For stream-based reading, we only parse .strings content
        // Plural support requires file-based reading to access .stringsdict
        var content = reader.ReadToEnd();
        var stringsEntries = _stringsParser.Parse(content);

        var entries = stringsEntries.Select(e => new ResourceEntry
        {
            Key = e.Key,
            Value = e.Value,
            Comment = e.Comment
        }).ToList();

        return new ResourceFile { Language = metadata, Entries = entries };
    }

    /// <inheritdoc />
    public Task<ResourceFile> ReadAsync(TextReader reader, LanguageInfo metadata, CancellationToken ct = default)
        => Task.FromResult(Read(reader, metadata));
}
