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

using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Configuration;

namespace LocalizationManager.Core.Backends.Json;

/// <summary>
/// JSON implementation of the resource backend.
/// Coordinates discovery, reading, writing, and validation of JSON localization files.
/// Supports both standard LRM format and i18next-compatible format with auto-detection.
/// </summary>
public class JsonResourceBackend : IResourceBackend
{
    private readonly JsonFormatConfiguration _config;
    private readonly JsonFormatDetector _detector = new();

    /// <inheritdoc />
    public string Name => "json";

    /// <inheritdoc />
    public IReadOnlyList<string> SupportedExtensions => new[] { ".json" };

    /// <inheritdoc />
    public IResourceDiscovery Discovery { get; }

    /// <inheritdoc />
    public IResourceReader Reader { get; }

    /// <inheritdoc />
    public IResourceWriter Writer { get; }

    /// <inheritdoc />
    public IResourceValidator Validator { get; }

    /// <summary>
    /// Creates a new JSON backend with optional configuration.
    /// </summary>
    /// <param name="config">JSON format configuration. If null, uses defaults.</param>
    public JsonResourceBackend(JsonFormatConfiguration? config = null)
    {
        _config = config ?? new JsonFormatConfiguration();
        Discovery = new JsonResourceDiscovery(_config);
        Reader = new JsonResourceReader(_config);
        Writer = new JsonResourceWriter(_config);
        Validator = new JsonResourceValidator(_config);
    }

    /// <summary>
    /// Creates a new JSON backend with auto-detection of format from the specified path.
    /// </summary>
    /// <param name="path">Path to the directory containing JSON files.</param>
    public JsonResourceBackend(string path)
    {
        var detected = _detector.Detect(path);
        _config = detected == DetectedJsonFormat.I18next
            ? CreateI18nextConfig()
            : new JsonFormatConfiguration();

        Discovery = new JsonResourceDiscovery(_config);
        Reader = new JsonResourceReader(_config);
        Writer = new JsonResourceWriter(_config);
        Validator = new JsonResourceValidator(_config);
    }

    /// <summary>
    /// Creates configuration optimized for i18next format.
    /// </summary>
    private static JsonFormatConfiguration CreateI18nextConfig() => new()
    {
        InterpolationFormat = "i18next",
        PluralFormat = "cldr",
        I18nextCompatible = true,
        UseNestedKeys = false  // i18next typically uses flat keys with namespace:key
    };
}
