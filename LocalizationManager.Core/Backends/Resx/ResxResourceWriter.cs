// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Resx;

/// <summary>
/// RESX implementation of resource writer.
/// Writes ResourceFile objects to .resx XML files.
/// </summary>
public class ResxResourceWriter : IResourceWriter
{
    /// <inheritdoc />
    public void Write(ResourceFile file)
    {
        try
        {
            // Load existing file to preserve structure
            // Only load if file exists AND has content (not empty)
            var filePath = file.Language.FilePath;
            if (string.IsNullOrEmpty(filePath))
            {
                throw new ArgumentException("FilePath is required for file-based writing", nameof(file));
            }

            var fileInfo = new FileInfo(filePath);
            XDocument xdoc;
            if (fileInfo.Exists && fileInfo.Length > 0)
            {
                using var xmlReader = XmlReader.Create(filePath, ResxResourceReader.CreateSecureXmlSettings());
                xdoc = XDocument.Load(xmlReader);
            }
            else
            {
                xdoc = CreateNewResxDocument();
            }

            var root = xdoc.Root;
            if (root == null)
            {
                throw new InvalidOperationException("Invalid XML structure: missing root element");
            }

            // Check if there are duplicate keys in file.Entries (case-insensitive per ResX specification)
            var hasDuplicates = file.Entries
                .GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
                .Any(g => g.Count() > 1);

            if (hasDuplicates)
            {
                // Handle duplicates: match by occurrence order
                WriteWithDuplicates(root, file.Entries);
            }
            else
            {
                // Use optimized dictionary approach for unique keys
                WriteWithUniqueKeys(root, file.Entries);
            }

            // Save with proper formatting
            xdoc.Save(file.Language.FilePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to write .resx file '{file.Language.FilePath}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public Task WriteAsync(ResourceFile file, CancellationToken ct = default)
    {
        Write(file);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task CreateLanguageFileAsync(
        string baseName,
        string cultureCode,
        string targetPath,
        ResourceFile? sourceFile = null,
        bool copyEntries = true,
        CancellationToken ct = default)
    {
        // Validate culture code
        if (!IsValidCultureCode(cultureCode, out var culture))
        {
            throw new ArgumentException($"Invalid culture code: {cultureCode}", nameof(cultureCode));
        }

        // Build target file path
        var fileName = $"{baseName}.{cultureCode}.resx";
        var filePath = Path.Combine(targetPath, fileName);

        // Check if file already exists
        if (File.Exists(filePath))
        {
            throw new InvalidOperationException($"Language file already exists: {fileName}");
        }

        // Create resource file with proper structure
        var resourceFile = new ResourceFile
        {
            Language = new LanguageInfo
            {
                BaseName = baseName,
                Code = cultureCode,
                Name = culture!.DisplayName,
                IsDefault = false,
                FilePath = filePath
            },
            Entries = new List<ResourceEntry>()
        };

        // Copy entries from source if specified
        if (copyEntries && sourceFile != null)
        {
            foreach (var entry in sourceFile.Entries)
            {
                resourceFile.Entries.Add(new ResourceEntry
                {
                    Key = entry.Key,
                    Value = entry.Value,
                    Comment = entry.Comment
                });
            }
        }

        // Write to disk
        Write(resourceFile);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteLanguageFileAsync(LanguageInfo language, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(language.FilePath) || !File.Exists(language.FilePath))
        {
            throw new FileNotFoundException($"Language file not found: {Path.GetFileName(language.FilePath)}");
        }

        // Prevent deletion of default language
        if (language.IsDefault)
        {
            throw new InvalidOperationException(
                "Cannot delete default language file. Default language files serve as fallback for all languages.");
        }

        File.Delete(language.FilePath);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public string SerializeToString(ResourceFile file)
    {
        var xdoc = CreateNewResxDocument();
        var root = xdoc.Root!;

        foreach (var entry in file.Entries)
        {
            root.Add(CreateDataElement(entry));
        }

        return xdoc.Declaration + Environment.NewLine + xdoc.ToString();
    }

    /// <summary>
    /// Writes entries when all keys are unique (optimized path).
    /// </summary>
    private void WriteWithUniqueKeys(XElement root, List<ResourceEntry> entries)
    {
        // Build a dictionary of new entries for quick lookup (case-insensitive per ResX specification)
        var newEntries = entries.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);
        var processedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Update existing data elements in place (preserves original order)
        foreach (var dataElement in root.Elements("data").ToList())
        {
            var key = dataElement.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(key)) continue;

            if (newEntries.TryGetValue(key, out var entry) && !processedKeys.Contains(key))
            {
                // Update the first occurrence
                UpdateDataElement(dataElement, entry);
                processedKeys.Add(key);
            }
            else
            {
                // Remove entries that are no longer present or are duplicate occurrences
                dataElement.Remove();
            }
        }

        // Add new entries that weren't in the original file (at the end)
        foreach (var entry in entries)
        {
            if (!processedKeys.Contains(entry.Key))
            {
                root.Add(CreateDataElement(entry));
            }
        }
    }

    /// <summary>
    /// Writes entries when there are duplicate keys (handles occurrence matching).
    /// </summary>
    private void WriteWithDuplicates(XElement root, List<ResourceEntry> entries)
    {
        // Track which entries have been written (case-insensitive per ResX specification)
        var entryIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var processedEntryIndices = new HashSet<int>();

        // Process existing data elements and match to entries by occurrence order
        var existingDataElements = root.Elements("data").ToList();
        var dataElementIndex = 0;

        foreach (var dataElement in existingDataElements)
        {
            var key = dataElement.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(key))
            {
                dataElementIndex++;
                continue;
            }

            // Find the Nth occurrence of this key in entries
            var occurrenceNumber = entryIndices.ContainsKey(key) ? entryIndices[key] + 1 : 1;
            entryIndices[key] = occurrenceNumber;

            // Find the corresponding entry (case-insensitive per ResX specification)
            var matchingEntries = entries
                .Select((e, i) => (e, i))
                .Where(x => x.e.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingEntries.Count >= occurrenceNumber)
            {
                // Update this occurrence
                var (entry, entryIndex) = matchingEntries[occurrenceNumber - 1];
                UpdateDataElement(dataElement, entry);
                processedEntryIndices.Add(entryIndex);
            }
            else
            {
                // This occurrence no longer exists in entries - remove it
                dataElement.Remove();
            }

            dataElementIndex++;
        }

        // Add new entries that weren't processed (at the end)
        for (int i = 0; i < entries.Count; i++)
        {
            if (!processedEntryIndices.Contains(i))
            {
                root.Add(CreateDataElement(entries[i]));
            }
        }
    }

    /// <summary>
    /// Updates an existing data element with new values.
    /// </summary>
    private static void UpdateDataElement(XElement dataElement, ResourceEntry entry)
    {
        var valueElement = dataElement.Element("value");
        if (valueElement != null)
        {
            valueElement.Value = entry.Value ?? string.Empty;
        }
        else
        {
            dataElement.Add(new XElement("value", entry.Value ?? string.Empty));
        }

        // Update or add comment
        var commentElement = dataElement.Element("comment");
        if (!string.IsNullOrEmpty(entry.Comment))
        {
            if (commentElement != null)
            {
                commentElement.Value = entry.Comment;
            }
            else
            {
                dataElement.Add(new XElement("comment", entry.Comment));
            }
        }
        else if (commentElement != null)
        {
            // Remove comment if it's now empty
            commentElement.Remove();
        }
    }

    /// <summary>
    /// Creates a new data element for an entry.
    /// </summary>
    private static XElement CreateDataElement(ResourceEntry entry)
    {
        var dataElement = new XElement("data",
            new XAttribute("name", entry.Key),
            new XAttribute(XNamespace.Xml + "space", "preserve"),
            new XElement("value", entry.Value ?? string.Empty));

        if (!string.IsNullOrEmpty(entry.Comment))
        {
            dataElement.Add(new XElement("comment", entry.Comment));
        }

        return dataElement;
    }

    /// <summary>
    /// Creates a new .resx XML document structure.
    /// </summary>
    private static XDocument CreateNewResxDocument()
    {
        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("root",
                new XElement("resheader",
                    new XAttribute("name", "resmimetype"),
                    new XElement("value", "text/microsoft-resx")),
                new XElement("resheader",
                    new XAttribute("name", "version"),
                    new XElement("value", "2.0")),
                new XElement("resheader",
                    new XAttribute("name", "reader"),
                    new XElement("value", "System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")),
                new XElement("resheader",
                    new XAttribute("name", "writer"),
                    new XElement("value", "System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"))
            )
        );
    }

    /// <summary>
    /// Validates a culture code.
    /// </summary>
    private static bool IsValidCultureCode(string code, out CultureInfo? culture)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(code);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = null;
            return false;
        }
    }
}
