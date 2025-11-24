// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LocalizationManager.Commands;

/// <summary>
/// Tracks the execution state of a command chain
/// </summary>
public class ChainExecutionContext
{
    /// <summary>
    /// Results for each executed command
    /// </summary>
    public List<ChainCommandResult> Results { get; set; } = new();

    /// <summary>
    /// Current step number (1-based)
    /// </summary>
    public int CurrentStep { get; set; }

    /// <summary>
    /// Total number of steps in the chain
    /// </summary>
    public int TotalSteps { get; set; }

    /// <summary>
    /// Whether to stop on first error
    /// </summary>
    public bool StopOnError { get; set; } = true;
}

/// <summary>
/// Result of executing a single command in the chain
/// </summary>
public class ChainCommandResult
{
    /// <summary>
    /// Command arguments that were executed
    /// </summary>
    public string[] CommandArgs { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Exit code returned by the command
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Status: "Success", "Failed", or "Skipped"
    /// </summary>
    public string Status { get; set; } = "Success";

    /// <summary>
    /// Duration of command execution
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Command display string (for logging/display)
    /// </summary>
    public string CommandString => string.Join(" ", CommandArgs);
}
