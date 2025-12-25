namespace LrmCloud.Shared.Constants;

/// <summary>
/// Organization role constants for role-based access control.
/// </summary>
public static class OrganizationRole
{
    /// <summary>
    /// Organization owner - full control including delete org, change plan, transfer ownership
    /// </summary>
    public const string Owner = "owner";

    /// <summary>
    /// Organization admin - can manage members, projects, and settings
    /// </summary>
    public const string Admin = "admin";

    /// <summary>
    /// Organization member - can view and edit projects
    /// </summary>
    public const string Member = "member";

    /// <summary>
    /// Organization viewer - read-only access
    /// </summary>
    public const string Viewer = "viewer";

    /// <summary>
    /// All valid roles
    /// </summary>
    public static readonly string[] All = { Owner, Admin, Member, Viewer };

    /// <summary>
    /// Check if a role is valid
    /// </summary>
    public static bool IsValid(string role)
    {
        return All.Contains(role);
    }

    /// <summary>
    /// Check if a role has admin privileges (owner or admin)
    /// </summary>
    public static bool IsAdminOrOwner(string? role)
    {
        if (string.IsNullOrEmpty(role)) return false;
        return string.Equals(role, Owner, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(role, Admin, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Check if a role is owner
    /// </summary>
    public static bool IsOwner(string? role)
    {
        if (string.IsNullOrEmpty(role)) return false;
        return string.Equals(role, Owner, StringComparison.OrdinalIgnoreCase);
    }
}
