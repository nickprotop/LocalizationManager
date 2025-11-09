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

using System.Xml.Linq;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core;

/// <summary>
/// Parses and manipulates .resx resource files.
/// </summary>
public class ResourceFileParser
{
    /// <summary>
    /// Reads and parses a .resx file into a ResourceFile object.
    /// </summary>
    /// <param name="language">Language information for the file being parsed.</param>
    /// <returns>Parsed resource file.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the XML is malformed.</exception>
    public ResourceFile Parse(LanguageInfo language)
    {
        if (!File.Exists(language.FilePath))
        {
            throw new FileNotFoundException($"Resource file not found: {language.FilePath}");
        }

        try
        {
            var xdoc = XDocument.Load(language.FilePath);
            var entries = new List<ResourceEntry>();

            // Parse data elements from .resx XML
            var dataElements = xdoc.Root?.Elements("data") ?? Enumerable.Empty<XElement>();

            foreach (var dataElement in dataElements)
            {
                var key = dataElement.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(key))
                {
                    continue; // Skip entries without a name
                }

                var value = dataElement.Element("value")?.Value;
                var comment = dataElement.Element("comment")?.Value;

                entries.Add(new ResourceEntry
                {
                    Key = key,
                    Value = value,
                    Comment = comment
                });
            }

            return new ResourceFile
            {
                Language = language,
                Entries = entries
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse .resx file '{language.FilePath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Writes a ResourceFile back to disk, preserving XML structure and original entry order.
    /// </summary>
    /// <param name="resourceFile">The resource file to write.</param>
    /// <exception cref="InvalidOperationException">Thrown when writing fails.</exception>
    public void Write(ResourceFile resourceFile)
    {
        try
        {
            // Load existing file to preserve structure
            var xdoc = File.Exists(resourceFile.Language.FilePath)
                ? XDocument.Load(resourceFile.Language.FilePath)
                : CreateNewResxDocument();

            var root = xdoc.Root;
            if (root == null)
            {
                throw new InvalidOperationException("Invalid XML structure: missing root element");
            }

            // Build a dictionary of new entries for quick lookup
            var newEntries = resourceFile.Entries.ToDictionary(e => e.Key);
            var processedKeys = new HashSet<string>();

            // Update existing data elements in place (preserves original order)
            foreach (var dataElement in root.Elements("data").ToList())
            {
                var key = dataElement.Attribute("name")?.Value;
                if (string.IsNullOrEmpty(key)) continue;

                if (newEntries.TryGetValue(key, out var entry))
                {
                    // Update existing entry in place
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

                    processedKeys.Add(key);
                }
                else
                {
                    // Remove entries that are no longer present
                    dataElement.Remove();
                }
            }

            // Add new entries that weren't in the original file (at the end)
            foreach (var entry in resourceFile.Entries)
            {
                if (!processedKeys.Contains(entry.Key))
                {
                    var dataElement = new XElement("data",
                        new XAttribute("name", entry.Key),
                        new XAttribute(XNamespace.Xml + "space", "preserve"),
                        new XElement("value", entry.Value ?? string.Empty));

                    if (!string.IsNullOrEmpty(entry.Comment))
                    {
                        dataElement.Add(new XElement("comment", entry.Comment));
                    }

                    root.Add(dataElement);
                }
            }

            // Save with proper formatting
            xdoc.Save(resourceFile.Language.FilePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to write .resx file '{resourceFile.Language.FilePath}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates a new .resx XML document structure.
    /// </summary>
    private XDocument CreateNewResxDocument()
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
}
