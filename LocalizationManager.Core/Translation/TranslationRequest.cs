// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace LocalizationManager.Core.Translation;

/// <summary>
/// Represents a translation request to be sent to a translation provider.
/// </summary>
public class TranslationRequest
{
    /// <summary>
    /// The text to translate.
    /// </summary>
    public required string SourceText { get; init; }

    /// <summary>
    /// The source language code (e.g., "en", "fr"). If null, the provider will attempt auto-detection.
    /// </summary>
    public string? SourceLanguage { get; init; }

    /// <summary>
    /// Optional display name for source language (e.g., "English", "French"). Used for better AI prompts.
    /// </summary>
    public string? SourceLanguageName { get; init; }

    /// <summary>
    /// The target language code (e.g., "fr", "de").
    /// </summary>
    public required string TargetLanguage { get; init; }

    /// <summary>
    /// Optional display name for target language (e.g., "French", "German"). Used for better AI prompts.
    /// </summary>
    public string? TargetLanguageName { get; init; }

    /// <summary>
    /// Optional context for the translation (e.g., resource key name, comments).
    /// </summary>
    public string? Context { get; init; }
}
