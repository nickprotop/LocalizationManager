// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Text;

namespace LocalizationManager.Commands;

/// <summary>
/// Parses command chains for the chain command.
/// Handles splitting by " -- " separator and quote-aware argument parsing.
/// </summary>
public static class ChainCommandParser
{
    private const string COMMAND_SEPARATOR = " -- ";

    /// <summary>
    /// Parse a command chain string into individual command argument arrays
    /// </summary>
    /// <param name="commandString">Full command chain string (e.g., "validate --format json -- translate --only-missing")</param>
    /// <returns>List of command argument arrays ready for CommandApp.Run()</returns>
    public static List<string[]> ParseChain(string commandString)
    {
        if (string.IsNullOrWhiteSpace(commandString))
        {
            return new List<string[]>();
        }

        // Split by " -- " separator
        var commandSegments = commandString.Split(new[] { COMMAND_SEPARATOR }, StringSplitOptions.None);

        var commands = new List<string[]>();

        foreach (var segment in commandSegments)
        {
            var trimmed = segment.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue; // Skip empty segments
            }

            // Parse arguments from segment
            var args = ParseArguments(trimmed);
            if (args.Length > 0)
            {
                commands.Add(args);
            }
        }

        return commands;
    }

    /// <summary>
    /// Parse a single command segment into arguments, respecting quotes
    /// </summary>
    /// <param name="commandSegment">Single command (e.g., "validate --format json")</param>
    /// <returns>Array of arguments</returns>
    public static string[] ParseArguments(string commandSegment)
    {
        if (string.IsNullOrWhiteSpace(commandSegment))
        {
            return Array.Empty<string>();
        }

        var tokens = new List<string>();
        var currentToken = new StringBuilder();
        bool inQuotes = false;
        bool escaped = false;

        for (int i = 0; i < commandSegment.Length; i++)
        {
            char c = commandSegment[i];

            // Handle escaped characters
            if (escaped)
            {
                currentToken.Append(c);
                escaped = false;
                continue;
            }

            // Check for escape character
            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            // Handle quote toggle
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            // Handle whitespace (only split if not in quotes)
            if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToString());
                    currentToken.Clear();
                }
                continue;
            }

            // Append character to current token
            currentToken.Append(c);
        }

        // Add final token if exists
        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        return tokens.ToArray();
    }

    /// <summary>
    /// Validate that a command chain string is well-formed
    /// </summary>
    public static (bool IsValid, string? ErrorMessage) ValidateChain(string commandString)
    {
        if (string.IsNullOrWhiteSpace(commandString))
        {
            return (false, "Command chain cannot be empty");
        }

        // Check for unmatched quotes
        int quoteCount = 0;
        bool escaped = false;

        foreach (char c in commandString)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                quoteCount++;
            }
        }

        if (quoteCount % 2 != 0)
        {
            return (false, "Unmatched quotes in command chain");
        }

        return (true, null);
    }
}
