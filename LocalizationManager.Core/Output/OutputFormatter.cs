using System.Text.Json;
using System.Text.Json.Serialization;

namespace LocalizationManager.Core.Output;

/// <summary>
/// Provides centralized formatting utilities for command output.
/// </summary>
public static class OutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Formats an object as JSON string.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="data">The object to serialize.</param>
    /// <returns>A formatted JSON string.</returns>
    public static string FormatJson<T>(T data)
    {
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    /// <summary>
    /// Parses a format string to OutputFormat enum.
    /// </summary>
    /// <param name="format">The format string (case-insensitive).</param>
    /// <param name="defaultFormat">The default format to return if parsing fails.</param>
    /// <returns>The parsed OutputFormat value.</returns>
    public static Enums.OutputFormat ParseFormat(string format, Enums.OutputFormat defaultFormat = Enums.OutputFormat.Table)
    {
        if (string.IsNullOrWhiteSpace(format))
            return defaultFormat;

        return format.ToLowerInvariant() switch
        {
            "table" => Enums.OutputFormat.Table,
            "json" => Enums.OutputFormat.Json,
            "simple" => Enums.OutputFormat.Simple,
            _ => defaultFormat
        };
    }
}
