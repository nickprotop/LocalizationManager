namespace LocalizationManager.Models.Api;

// Request
public class CreateBackupRequest
{
    public string? FileName { get; set; }
    public string? Operation { get; set; }
}

public class RestoreBackupRequest
{
    public bool Preview { get; set; }
    public List<string>? Keys { get; set; }
}

// Response
public class BackupListResponse
{
    public string? FileName { get; set; }
    public int BackupCount { get; set; }
    public List<BackupInfo> Backups { get; set; } = new();
}

public class BackupInfo
{
    public string FileName { get; set; } = string.Empty;
    public int Version { get; set; }
    public DateTime Timestamp { get; set; }
    public string Operation { get; set; } = string.Empty;
    public int KeyCount { get; set; }
    public int ChangedKeys { get; set; }
    public List<string>? ChangedKeyNames { get; set; }
}

public class CreateBackupResponse
{
    public bool Success { get; set; }
    public string? FileName { get; set; }
    public int? Version { get; set; }
    public string? Operation { get; set; }
    public List<BackupCreatedInfo>? Backups { get; set; }
}

public class BackupCreatedInfo
{
    public string FileName { get; set; } = string.Empty;
    public int Version { get; set; }
}

public class RestoreBackupResponse
{
    public bool Preview { get; set; }
    public bool Success { get; set; }
    public string? FileName { get; set; }
    public int? Version { get; set; }
    public int? RestoredKeys { get; set; }
    public string? Message { get; set; }
    public int AddedCount { get; set; }
    public int ModifiedCount { get; set; }
    public int RemovedCount { get; set; }
    public RestoreChanges? Changes { get; set; }
}

public class RestoreChanges
{
    public List<string> Added { get; set; } = new();
    public List<ModifiedKeyInfo> Modified { get; set; } = new();
    public List<string> Removed { get; set; } = new();
}

public class ModifiedKeyInfo
{
    public string Key { get; set; } = string.Empty;
    public string? CurrentValue { get; set; }
    public string? BackupValue { get; set; }
}

public class DeleteBackupResponse
{
    public bool Success { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Message { get; set; } = string.Empty;
}
