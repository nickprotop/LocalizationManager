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
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Resx;

/// <summary>
/// RESX implementation of resource writer.
/// Wraps the existing ResourceFileParser and LanguageFileManager classes.
/// </summary>
public class ResxResourceWriter : IResourceWriter
{
    private readonly ResourceFileParser _parser = new();
    private readonly LanguageFileManager _fileManager = new();

    /// <inheritdoc />
    public void Write(ResourceFile file)
        => _parser.Write(file);

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
        _fileManager.CreateLanguageFile(baseName, cultureCode, targetPath, sourceFile, copyEntries);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteLanguageFileAsync(LanguageInfo language, CancellationToken ct = default)
    {
        _fileManager.DeleteLanguageFile(language);
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
}
