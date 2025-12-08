// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

namespace LrmCloud.Shared.DTOs.Projects;

/// <summary>
/// DTO for project configuration (lrm.json).
/// </summary>
public class ConfigurationDto
{
    public string ConfigJson { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Request to update project configuration.
/// </summary>
public class UpdateConfigurationRequest
{
    public required string ConfigJson { get; set; }
    public string? BaseVersion { get; set; }
}

/// <summary>
/// Configuration history entry.
/// </summary>
public class ConfigurationHistoryDto
{
    public string Version { get; set; } = string.Empty;
    public string ConfigJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    public string? Message { get; set; }
}

/// <summary>
/// Response for configuration sync status.
/// </summary>
public class SyncStatusDto
{
    public bool IsSynced { get; set; }
    public DateTime? LastPush { get; set; }
    public DateTime? LastPull { get; set; }
    public int LocalChanges { get; set; }
    public int RemoteChanges { get; set; }
}
