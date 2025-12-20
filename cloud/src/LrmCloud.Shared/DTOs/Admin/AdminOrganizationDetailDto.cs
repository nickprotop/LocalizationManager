namespace LrmCloud.Shared.DTOs.Admin;

/// <summary>
/// Detailed organization information for admin detail view.
/// </summary>
public class AdminOrganizationDetailDto : AdminOrganizationDto
{
    /// <summary>
    /// Organization plan (team, enterprise).
    /// </summary>
    public string Plan { get; set; } = "";

    /// <summary>
    /// Translation characters used this period.
    /// </summary>
    public int TranslationCharsUsed { get; set; }

    /// <summary>
    /// Translation characters limit.
    /// </summary>
    public int TranslationCharsLimit { get; set; }

    /// <summary>
    /// Payment provider (stripe, paypal).
    /// </summary>
    public string? PaymentProvider { get; set; }

    /// <summary>
    /// Payment customer ID.
    /// </summary>
    public string? PaymentCustomerId { get; set; }

    /// <summary>
    /// Organization members.
    /// </summary>
    public List<AdminOrgMemberDto> Members { get; set; } = new();

    /// <summary>
    /// Organization projects.
    /// </summary>
    public List<AdminOrgProjectDto> Projects { get; set; } = new();
}

/// <summary>
/// Organization member for admin view.
/// </summary>
public class AdminOrgMemberDto
{
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = "";
    public DateTime JoinedAt { get; set; }
    public bool IsOwner { get; set; }
}

/// <summary>
/// Organization project for admin view.
/// </summary>
public class AdminOrgProjectDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int LanguageCount { get; set; }
    public int KeyCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to transfer organization ownership.
/// </summary>
public class AdminTransferOwnershipRequest
{
    /// <summary>
    /// New owner user ID.
    /// </summary>
    public int NewOwnerId { get; set; }
}

/// <summary>
/// Request to update organization properties.
/// </summary>
public class AdminUpdateOrganizationDto
{
    /// <summary>
    /// New organization name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// New organization plan.
    /// </summary>
    public string? Plan { get; set; }

    /// <summary>
    /// New translation characters limit.
    /// </summary>
    public int? TranslationCharsLimit { get; set; }

    /// <summary>
    /// Reset translation usage to 0.
    /// </summary>
    public bool? ResetUsage { get; set; }
}
