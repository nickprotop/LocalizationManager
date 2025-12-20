namespace LrmCloud.Shared.DTOs.Admin;

/// <summary>
/// User information for admin list view.
/// </summary>
public class AdminUserDto
{
    public int Id { get; set; }
    public string? Email { get; set; }
    public string Username { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string AuthType { get; set; } = "email";
    public string Plan { get; set; } = "free";
    public string SubscriptionStatus { get; set; } = "none";
    public bool IsSuperAdmin { get; set; }
    public bool EmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

/// <summary>
/// Detailed user information for admin detail view.
/// </summary>
public class AdminUserDetailDto : AdminUserDto
{
    // Billing
    public string? PaymentProvider { get; set; }
    public string? PaymentCustomerId { get; set; }
    public string? PaymentSubscriptionId { get; set; }
    public DateTime? SubscriptionCurrentPeriodEnd { get; set; }
    public bool CancelAtPeriodEnd { get; set; }

    // Usage
    public int TranslationCharsUsed { get; set; }
    public int TranslationCharsLimit { get; set; }
    public long OtherCharsUsed { get; set; }
    public long OtherCharsLimit { get; set; }
    public DateTime? TranslationCharsResetAt { get; set; }
    public DateTime? OtherCharsResetAt { get; set; }

    // Stats
    public int ProjectCount { get; set; }
    public int OrganizationCount { get; set; }

    // Security
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to update a user's admin-controlled properties.
/// </summary>
public class AdminUpdateUserDto
{
    public string? Plan { get; set; }  // free, team, enterprise
    public int? TranslationCharsLimit { get; set; }
    public long? OtherCharsLimit { get; set; }
    public bool? IsSuperAdmin { get; set; }
    public bool? EmailVerified { get; set; }
    public string? SubscriptionStatus { get; set; }
}
