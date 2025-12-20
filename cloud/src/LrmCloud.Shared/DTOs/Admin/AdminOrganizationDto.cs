namespace LrmCloud.Shared.DTOs.Admin;

/// <summary>
/// Organization information for admin list view.
/// </summary>
public class AdminOrganizationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public int MemberCount { get; set; }
    public int ProjectCount { get; set; }
    public int OwnerId { get; set; }
    public string? OwnerEmail { get; set; }
    public string? OwnerUsername { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
