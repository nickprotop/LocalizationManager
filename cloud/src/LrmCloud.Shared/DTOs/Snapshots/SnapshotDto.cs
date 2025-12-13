namespace LrmCloud.Shared.DTOs.Snapshots;

/// <summary>
/// DTO for listing snapshots.
/// </summary>
public class SnapshotDto
{
    public int Id { get; set; }
    public required string SnapshotId { get; set; }
    public int ProjectId { get; set; }
    public string? Description { get; set; }
    public required string SnapshotType { get; set; }
    public int FileCount { get; set; }
    public int KeyCount { get; set; }
    public int TranslationCount { get; set; }
    public int? CreatedByUserId { get; set; }
    public string? CreatedByUsername { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for snapshot details including file list.
/// </summary>
public class SnapshotDetailDto : SnapshotDto
{
    public List<SnapshotFileDto> Files { get; set; } = new();
}

/// <summary>
/// DTO for a file within a snapshot.
/// </summary>
public class SnapshotFileDto
{
    public required string Path { get; set; }
    public required string LanguageCode { get; set; }
    public long Size { get; set; }
    public string? Content { get; set; }
}

/// <summary>
/// DTO for comparing two snapshots.
/// </summary>
public class SnapshotDiffDto
{
    public required string FromSnapshotId { get; set; }
    public required string ToSnapshotId { get; set; }
    public List<SnapshotDiffFileDto> Files { get; set; } = new();
    public int KeysAdded { get; set; }
    public int KeysRemoved { get; set; }
    public int KeysModified { get; set; }
}

/// <summary>
/// DTO for a file difference in snapshot comparison.
/// </summary>
public class SnapshotDiffFileDto
{
    public required string Path { get; set; }
    public required string ChangeType { get; set; } // "added", "removed", "modified"
    public string? OldContent { get; set; }
    public string? NewContent { get; set; }
}

/// <summary>
/// Request to create a manual snapshot.
/// </summary>
public class CreateSnapshotRequest
{
    public string? Description { get; set; }
}

/// <summary>
/// Request to restore from a snapshot.
/// </summary>
public class RestoreSnapshotRequest
{
    /// <summary>
    /// Optional message describing the restore operation.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Whether to create a backup snapshot before restoring.
    /// Defaults to true.
    /// </summary>
    public bool CreateBackup { get; set; } = true;
}
