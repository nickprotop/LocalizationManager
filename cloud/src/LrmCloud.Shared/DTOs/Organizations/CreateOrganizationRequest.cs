using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Organizations;

/// <summary>
/// Request to create a new organization.
/// </summary>
public class CreateOrganizationRequest
{
    [Required(ErrorMessage = "Organization name is required")]
    [MaxLength(255, ErrorMessage = "Organization name must not exceed 255 characters")]
    public required string Name { get; set; }

    [MaxLength(255, ErrorMessage = "Slug must not exceed 255 characters")]
    public string? Slug { get; set; }  // Optional, auto-generated from name if not provided

    [MaxLength(1000, ErrorMessage = "Description must not exceed 1000 characters")]
    public string? Description { get; set; }
}
