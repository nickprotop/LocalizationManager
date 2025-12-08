using System.ComponentModel.DataAnnotations;

namespace LrmCloud.Shared.DTOs.Organizations;

/// <summary>
/// Request to update an organization.
/// </summary>
public class UpdateOrganizationRequest
{
    [MaxLength(255, ErrorMessage = "Organization name must not exceed 255 characters")]
    public string? Name { get; set; }

    [MaxLength(1000, ErrorMessage = "Description must not exceed 1000 characters")]
    public string? Description { get; set; }
}
