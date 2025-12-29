// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Xml;

namespace LocalizationManager.Core.Backends.Xliff;

/// <summary>
/// Detects XLIFF version from file content.
/// Supports XLIFF 1.2 and 2.0 formats.
/// </summary>
public class XliffVersionDetector
{
    /// <summary>
    /// XLIFF 1.2 namespace URI.
    /// </summary>
    public const string Xliff12Namespace = "urn:oasis:names:tc:xliff:document:1.2";

    /// <summary>
    /// XLIFF 2.0 namespace URI.
    /// </summary>
    public const string Xliff20Namespace = "urn:oasis:names:tc:xliff:document:2.0";

    /// <summary>
    /// Detects the XLIFF version from a file.
    /// </summary>
    /// <param name="filePath">Path to the XLIFF file.</param>
    /// <returns>Detected version ("1.2", "2.0", or "unknown").</returns>
    public string DetectVersion(string filePath)
    {
        if (!File.Exists(filePath))
            return "unknown";

        try
        {
            using var stream = File.OpenRead(filePath);
            return DetectVersion(stream);
        }
        catch
        {
            return "unknown";
        }
    }

    /// <summary>
    /// Detects the XLIFF version from a stream.
    /// </summary>
    /// <param name="stream">Stream containing XLIFF content.</param>
    /// <returns>Detected version ("1.2", "2.0", or "unknown").</returns>
    public string DetectVersion(Stream stream)
    {
        try
        {
            // Use safe XML settings to prevent XXE attacks
            var settings = CreateSafeXmlReaderSettings();
            using var reader = XmlReader.Create(stream, settings);

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "xliff")
                {
                    // Check namespace first
                    var ns = reader.NamespaceURI;
                    if (ns.Contains("2.0"))
                        return "2.0";
                    if (ns.Contains("1.2"))
                        return "1.2";

                    // Check version attribute as fallback
                    var version = reader.GetAttribute("version");
                    if (!string.IsNullOrEmpty(version))
                    {
                        if (version.StartsWith("2"))
                            return "2.0";
                        if (version.StartsWith("1"))
                            return "1.2";
                    }

                    // Check for srcLang (XLIFF 2.0) vs source-language (XLIFF 1.2)
                    if (reader.GetAttribute("srcLang") != null)
                        return "2.0";
                    if (reader.GetAttribute("source-language") != null)
                        return "1.2";

                    break;
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return "unknown";
    }

    /// <summary>
    /// Creates safe XML reader settings to prevent XXE attacks.
    /// </summary>
    public static XmlReaderSettings CreateSafeXmlReaderSettings()
    {
        return new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            IgnoreWhitespace = false,
            IgnoreComments = false
        };
    }
}

/// <summary>
/// XLIFF version enumeration.
/// </summary>
public enum XliffVersion
{
    /// <summary>
    /// Unknown or unsupported version.
    /// </summary>
    Unknown,

    /// <summary>
    /// XLIFF 1.2 (OASIS standard).
    /// </summary>
    V12,

    /// <summary>
    /// XLIFF 2.0 (OASIS standard).
    /// </summary>
    V20
}
