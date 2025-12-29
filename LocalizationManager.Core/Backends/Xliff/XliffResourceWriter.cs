// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;

namespace LocalizationManager.Core.Backends.Xliff;

/// <summary>
/// Writes ResourceFile objects to XLIFF format files.
/// Supports both XLIFF 1.2 and 2.0 formats.
/// </summary>
public class XliffResourceWriter : IResourceWriter
{
    private readonly XliffFormatConfiguration _config;

    private static readonly XNamespace Ns12 = XliffVersionDetector.Xliff12Namespace;
    private static readonly XNamespace Ns20 = XliffVersionDetector.Xliff20Namespace;

    public XliffResourceWriter(XliffFormatConfiguration? config = null)
    {
        _config = config ?? new XliffFormatConfiguration();
    }

    /// <inheritdoc />
    public async Task WriteAsync(ResourceFile file, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(file.Language.FilePath))
            throw new ArgumentException("FilePath is required for file-based writing", nameof(file));

        var content = SerializeToString(file);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(file.Language.FilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        // Atomic write
        var tempPath = file.Language.FilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8, ct);
        File.Move(tempPath, file.Language.FilePath, overwrite: true);
    }

    /// <inheritdoc />
    public void Write(ResourceFile file)
    {
        WriteAsync(file).GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task CreateLanguageFileAsync(
        string baseName,
        string cultureCode,
        string targetPath,
        ResourceFile? sourceFile = null,
        bool copyEntries = true,
        CancellationToken ct = default)
    {
        var extension = _config.FileExtension.StartsWith(".")
            ? _config.FileExtension
            : "." + _config.FileExtension;
        var filePath = Path.Combine(targetPath, $"{baseName}.{cultureCode}{extension}");

        var entries = new List<ResourceEntry>();
        if (copyEntries && sourceFile != null)
        {
            // Copy entries with empty values
            entries = sourceFile.Entries.Select(e => new ResourceEntry
            {
                Key = e.Key,
                Value = "",
                Comment = e.Comment,
                IsPlural = e.IsPlural,
                PluralForms = e.IsPlural
                    ? e.PluralForms?.ToDictionary(kv => kv.Key, kv => "")
                    : null
            }).ToList();
        }

        var languageInfo = new LanguageInfo
        {
            Code = cultureCode,
            Name = GetLanguageDisplayName(cultureCode),
            BaseName = baseName,
            FilePath = filePath,
            IsDefault = false
        };

        var file = new ResourceFile
        {
            Language = languageInfo,
            Entries = entries
        };

        await WriteAsync(file, ct);
    }

    /// <inheritdoc />
    public Task DeleteLanguageFileAsync(LanguageInfo language, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(language.FilePath) && File.Exists(language.FilePath))
        {
            File.Delete(language.FilePath);
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public string SerializeToString(ResourceFile file)
    {
        var doc = _config.Version == "2.0"
            ? CreateXliff20Document(file)
            : CreateXliff12Document(file);

        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        };

        using var sw = new StringWriter();
        using (var writer = XmlWriter.Create(sw, settings))
        {
            doc.Save(writer);
        } // XmlWriter is disposed and flushed here
        return sw.ToString();
    }

    /// <summary>
    /// Creates an XLIFF 1.2 document.
    /// </summary>
    private XDocument CreateXliff12Document(ResourceFile file)
    {
        var root = new XElement(Ns12 + "xliff",
            new XAttribute("version", "1.2"),
            new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
            new XAttribute("xmlns", Ns12.NamespaceName));

        var sourceLanguage = file.Language.IsDefault ? file.Language.Code : "en";
        var targetLanguage = file.Language.IsDefault ? "" : file.Language.Code;

        var fileElement = new XElement(Ns12 + "file",
            new XAttribute("original", file.Language.BaseName ?? "resources"),
            new XAttribute("source-language", sourceLanguage),
            new XAttribute("datatype", "plaintext"));

        if (!string.IsNullOrEmpty(targetLanguage))
        {
            fileElement.Add(new XAttribute("target-language", targetLanguage));
        }

        var header = new XElement(Ns12 + "header",
            new XElement(Ns12 + "tool",
                new XAttribute("tool-id", "lrm"),
                new XAttribute("tool-name", "Localization Resource Manager"),
                new XAttribute("tool-version", "1.0")));
        fileElement.Add(header);

        var body = new XElement(Ns12 + "body");

        foreach (var entry in file.Entries)
        {
            if (entry.IsPlural && entry.PluralForms != null)
            {
                body.Add(CreatePluralGroup12(entry));
            }
            else
            {
                body.Add(CreateTransUnit12(entry, file.Language.IsDefault));
            }
        }

        fileElement.Add(body);
        root.Add(fileElement);

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            root);
    }

    /// <summary>
    /// Creates a trans-unit element for XLIFF 1.2.
    /// </summary>
    private XElement CreateTransUnit12(ResourceEntry entry, bool isDefault)
    {
        var unit = new XElement(Ns12 + "trans-unit",
            new XAttribute("id", entry.Key));

        // Source is the key for default language, or include bilingual content
        if (isDefault || _config.Bilingual)
        {
            unit.Add(new XElement(Ns12 + "source", entry.Value ?? ""));
        }
        else
        {
            unit.Add(new XElement(Ns12 + "source", entry.Key));
            unit.Add(new XElement(Ns12 + "target", entry.Value ?? ""));
        }

        if (!string.IsNullOrEmpty(entry.Comment))
        {
            unit.Add(new XElement(Ns12 + "note", entry.Comment));
        }

        return unit;
    }

    /// <summary>
    /// Creates a plural group for XLIFF 1.2.
    /// </summary>
    private XElement CreatePluralGroup12(ResourceEntry entry)
    {
        var group = new XElement(Ns12 + "group",
            new XAttribute("id", entry.Key),
            new XAttribute("restype", "x-gettext-plurals"));

        if (!string.IsNullOrEmpty(entry.Comment))
        {
            group.Add(new XElement(Ns12 + "note", entry.Comment));
        }

        if (entry.PluralForms != null)
        {
            foreach (var (category, value) in entry.PluralForms)
            {
                var unit = new XElement(Ns12 + "trans-unit",
                    new XAttribute("id", $"{entry.Key}[{category}]"),
                    new XElement(Ns12 + "source", entry.Key),
                    new XElement(Ns12 + "target", value ?? ""));
                group.Add(unit);
            }
        }

        return group;
    }

    /// <summary>
    /// Creates an XLIFF 2.0 document.
    /// </summary>
    private XDocument CreateXliff20Document(ResourceFile file)
    {
        var sourceLanguage = file.Language.IsDefault ? file.Language.Code : "en";
        var targetLanguage = file.Language.IsDefault ? sourceLanguage : file.Language.Code;

        var root = new XElement(Ns20 + "xliff",
            new XAttribute("version", "2.0"),
            new XAttribute("srcLang", sourceLanguage),
            new XAttribute("trgLang", targetLanguage),
            new XAttribute("xmlns", Ns20.NamespaceName));

        var fileElement = new XElement(Ns20 + "file",
            new XAttribute("id", file.Language.BaseName ?? "resources"));

        foreach (var entry in file.Entries)
        {
            fileElement.Add(CreateUnit20(entry, file.Language.IsDefault));
        }

        root.Add(fileElement);

        return new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            root);
    }

    /// <summary>
    /// Creates a unit element for XLIFF 2.0.
    /// </summary>
    private XElement CreateUnit20(ResourceEntry entry, bool isDefault)
    {
        var unit = new XElement(Ns20 + "unit",
            new XAttribute("id", entry.Key));

        if (!string.IsNullOrEmpty(entry.Comment))
        {
            var notes = new XElement(Ns20 + "notes",
                new XElement(Ns20 + "note", entry.Comment));
            unit.Add(notes);
        }

        if (entry.IsPlural && entry.PluralForms != null)
        {
            // Create multiple segments for plurals
            foreach (var (category, value) in entry.PluralForms)
            {
                var segment = new XElement(Ns20 + "segment",
                    new XAttribute("id", category),
                    new XElement(Ns20 + "source", entry.Key),
                    new XElement(Ns20 + "target", value ?? ""));
                unit.Add(segment);
            }
        }
        else
        {
            var segment = new XElement(Ns20 + "segment");

            if (isDefault)
            {
                segment.Add(new XElement(Ns20 + "source", entry.Value ?? ""));
            }
            else
            {
                segment.Add(new XElement(Ns20 + "source", entry.Key));
                segment.Add(new XElement(Ns20 + "target", entry.Value ?? ""));
            }

            unit.Add(segment);
        }

        return unit;
    }

    private string GetLanguageDisplayName(string cultureCode)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(cultureCode);
            return $"{culture.DisplayName} ({cultureCode})";
        }
        catch
        {
            return cultureCode;
        }
    }
}
