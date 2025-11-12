// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LocalizationManager.Core.Translation;

/// <summary>
/// Interface for translation providers.
/// </summary>
public interface ITranslationProvider
{
    /// <summary>
    /// Gets the name of the provider (e.g., "google", "deepl", "libretranslate").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Translates a single text.
    /// </summary>
    /// <param name="request">The translation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The translation response.</returns>
    Task<TranslationResponse> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Translates multiple texts in a batch.
    /// </summary>
    /// <param name="requests">The translation requests.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The translation responses in the same order as the requests.</returns>
    Task<IReadOnlyList<TranslationResponse>> TranslateBatchAsync(
        IEnumerable<TranslationRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the provider is properly configured (e.g., has API key).
    /// </summary>
    /// <returns>True if configured, otherwise false.</returns>
    bool IsConfigured();

    /// <summary>
    /// Gets the rate limit for this provider (requests per minute).
    /// </summary>
    /// <returns>The rate limit, or null if unlimited/unknown.</returns>
    int? GetRateLimit();

    /// <summary>
    /// Gets the supported source languages.
    /// </summary>
    /// <returns>List of supported language codes, or null if all languages are supported.</returns>
    Task<IReadOnlyList<string>?> GetSupportedSourceLanguagesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the supported target languages.
    /// </summary>
    /// <returns>List of supported language codes, or null if all languages are supported.</returns>
    Task<IReadOnlyList<string>?> GetSupportedTargetLanguagesAsync(
        CancellationToken cancellationToken = default);
}
