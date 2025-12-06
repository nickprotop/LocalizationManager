// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace LocalizationManager.Core.Translation;

/// <summary>
/// Represents a response from a translation provider.
/// </summary>
public class TranslationResponse
{
    /// <summary>
    /// The translated text.
    /// </summary>
    public required string TranslatedText { get; init; }

    /// <summary>
    /// The detected source language (if auto-detection was used).
    /// </summary>
    public string? DetectedSourceLanguage { get; init; }

    /// <summary>
    /// Confidence score of the translation (0.0 to 1.0), if provided by the API.
    /// </summary>
    public double? Confidence { get; init; }

    /// <summary>
    /// The provider that generated this translation.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Indicates whether the response was retrieved from cache.
    /// </summary>
    public bool FromCache { get; init; }
}
