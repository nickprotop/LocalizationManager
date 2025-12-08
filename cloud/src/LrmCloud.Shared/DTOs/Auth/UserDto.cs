namespace LrmCloud.Shared.DTOs.Auth;

public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public bool EmailVerified { get; set; }
    public string Plan { get; set; } = "free";
    public DateTime CreatedAt { get; set; }
}
