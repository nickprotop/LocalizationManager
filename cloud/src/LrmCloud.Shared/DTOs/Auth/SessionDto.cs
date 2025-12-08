namespace LrmCloud.Shared.DTOs.Auth;

/// <summary>
/// Represents an active user session (refresh token).
/// </summary>
public class SessionDto
{
    /// <summary>
    /// Session ID (refresh token ID)
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// When this session was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this session expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// When this session was last used to refresh tokens
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// IP address that created this session
    /// </summary>
    public string? CreatedByIp { get; set; }

    /// <summary>
    /// Whether this is the current session (the one being used for this request)
    /// </summary>
    public bool IsCurrent { get; set; }
}
