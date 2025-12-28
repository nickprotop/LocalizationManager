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

using System.Xml;
using System.Xml.Linq;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Resx;

/// <summary>
/// RESX implementation of resource reader.
/// Parses .resx XML files into ResourceFile objects.
/// </summary>
public class ResxResourceReader : IResourceReader
{
    /// <summary>
    /// Creates secure XML reader settings to prevent XXE and XML bomb attacks.
    /// </summary>
    internal static XmlReaderSettings CreateSecureXmlSettings()
    {
        return new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,  // Block DTD processing (prevents XXE attacks)
            XmlResolver = null,                       // Prevent external resource resolution
            MaxCharactersFromEntities = 0,            // Block entity expansion (prevents XML bombs)
            MaxCharactersInDocument = 10_000_000      // 10MB limit for document size
        };
    }

    /// <inheritdoc />
    public ResourceFile Read(LanguageInfo language)
    {
        if (string.IsNullOrEmpty(language.FilePath))
        {
            throw new ArgumentException("FilePath is required for file-based parsing", nameof(language));
        }

        if (!File.Exists(language.FilePath))
        {
            throw new FileNotFoundException($"Resource file not found: {language.FilePath}");
        }

        try
        {
            using var xmlReader = XmlReader.Create(language.FilePath, CreateSecureXmlSettings());
            var xdoc = XDocument.Load(xmlReader);
            return ParseXDocument(xdoc, language);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse .resx file '{language.FilePath}': {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public Task<ResourceFile> ReadAsync(LanguageInfo language, CancellationToken ct = default)
        => Task.FromResult(Read(language));

    /// <inheritdoc />
    public ResourceFile Read(TextReader reader, LanguageInfo metadata)
    {
        try
        {
            using var xmlReader = XmlReader.Create(reader, CreateSecureXmlSettings());
            var xdoc = XDocument.Load(xmlReader);
            return ParseXDocument(xdoc, metadata);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse .resx content: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public Task<ResourceFile> ReadAsync(TextReader reader, LanguageInfo metadata, CancellationToken ct = default)
        => Task.FromResult(Read(reader, metadata));

    /// <summary>
    /// Internal method to parse an XDocument into a ResourceFile.
    /// </summary>
    private static ResourceFile ParseXDocument(XDocument xdoc, LanguageInfo language)
    {
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
}
