namespace LrmCloud.Shared.DTOs.Organizations;

/// <summary>
/// Organization data transfer object.
/// </summary>
public class OrganizationDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public int OwnerId { get; set; }
    public required string Plan { get; set; }
    public int MemberCount { get; set; }
    public required string UserRole { get; set; }  // Current user's role in this org
    public DateTime CreatedAt { get; set; }
}
