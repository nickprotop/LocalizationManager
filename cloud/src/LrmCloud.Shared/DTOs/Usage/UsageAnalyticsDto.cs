namespace LrmCloud.Shared.DTOs.Usage;

/// <summary>
/// User's usage broken down by personal projects vs each organization they contribute to.
/// </summary>
public class UserUsageBreakdownDto
{
    /// <summary>
    /// LRM characters used on personal projects.
    /// </summary>
    public long PersonalLrmChars { get; set; }

    /// <summary>
    /// BYOK characters used on personal projects.
    /// </summary>
    public long PersonalByokChars { get; set; }

    /// <summary>
    /// Usage contributions to each organization the user is a member of.
    /// </summary>
    public List<OrgUsageContributionDto> OrganizationContributions { get; set; } = new();
}

/// <summary>
/// User's usage contribution to a specific organization.
/// </summary>
public class OrgUsageContributionDto
{
    public int OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public long LrmCharsUsed { get; set; }
    public long ByokCharsUsed { get; set; }
}

/// <summary>
/// Usage breakdown by member for organization admins/owners.
/// </summary>
public class OrgMemberUsageDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public long LrmCharsUsed { get; set; }
    public long ByokCharsUsed { get; set; }

    /// <summary>
    /// Total characters used (LRM + BYOK).
    /// </summary>
    public long TotalCharsUsed => LrmCharsUsed + ByokCharsUsed;
}

/// <summary>
/// Usage breakdown for a specific project.
/// </summary>
public class ProjectUsageDto
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public long TotalLrmChars { get; set; }
    public long TotalByokChars { get; set; }

    /// <summary>
    /// Usage breakdown by each contributor to the project.
    /// </summary>
    public List<ProjectMemberUsageDto> MemberBreakdown { get; set; } = new();
}

/// <summary>
/// Individual member's usage on a project.
/// </summary>
public class ProjectMemberUsageDto
{
    public int UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public long LrmCharsUsed { get; set; }
    public long ByokCharsUsed { get; set; }

    /// <summary>
    /// Total characters used (LRM + BYOK).
    /// </summary>
    public long TotalCharsUsed => LrmCharsUsed + ByokCharsUsed;
}
