// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Core.Cloud;

/// <summary>
/// Exception thrown when a cloud API operation fails.
/// </summary>
public class CloudApiException : Exception
{
    /// <summary>
    /// HTTP status code if available.
    /// </summary>
    public int? StatusCode { get; }

    public CloudApiException(string message) : base(message)
    {
    }

    public CloudApiException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public CloudApiException(string message, Exception innerException) : base(message, innerException)
    {
    }

    public CloudApiException(string message, int statusCode, Exception innerException)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
