using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Auth;

public class UpdateProfileRequest
{
    /// <summary>
    /// Username (3-50 chars, alphanumeric + underscore/hyphen)
    /// </summary>
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters")]
    [RegularExpression(@"^[a-zA-Z0-9_-]+$", ErrorMessage = "Username can only contain letters, numbers, underscores, and hyphens")]
    public string? Username { get; set; }

    /// <summary>
    /// Display name (optional, max 255 chars)
    /// </summary>
    [StringLength(255, ErrorMessage = "Display name cannot exceed 255 characters")]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Avatar URL (optional, must be valid URL)
    /// </summary>
    [Url(ErrorMessage = "Avatar URL must be a valid URL")]
    [StringLength(500, ErrorMessage = "Avatar URL cannot exceed 500 characters")]
    public string? AvatarUrl { get; set; }
}
