using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LrmCloud.Shared.Configuration;

/// <summary>
/// Root configuration class for LRM Cloud.
/// Maps to config.json structure with full type safety.
/// </summary>
public sealed class CloudConfiguration
{
    [JsonPropertyName("$schema")]
    public string? Schema { get; init; }

    [Required]
    public required ServerConfiguration Server { get; init; }

    [Required]
    public required DatabaseConfiguration Database { get; init; }

    [Required]
    public required RedisConfiguration Redis { get; init; }

    [Required]
    public required StorageConfiguration Storage { get; init; }

    [Required]
    public required EncryptionConfiguration Encryption { get; init; }

    [Required]
    public required AuthConfiguration Auth { get; init; }

    [Required]
    public required MailConfiguration Mail { get; init; }

    [Required]
    public required FeaturesConfiguration Features { get; init; }

    [Required]
    public required LimitsConfiguration Limits { get; init; }

    /// <summary>
    /// Master secret for encrypting translation provider API keys.
    /// Should be a long random string (32+ characters).
    /// </summary>
    public string? ApiKeyMasterSecret { get; init; }

    /// <summary>
    /// LRM managed translation provider configuration.
    /// </summary>
    public LrmProviderConfiguration LrmProvider { get; init; } = new();
}

/// <summary>
/// Server configuration (URLs, environment).
/// </summary>
public sealed class ServerConfiguration
{
    /// <summary>
    /// URLs to listen on. Example: "http://0.0.0.0:8080"
    /// </summary>
    [Required]
    public required string Urls { get; init; }

    /// <summary>
    /// ASP.NET environment. Example: "Production", "Development"
    /// </summary>
    [Required]
    public required string Environment { get; init; }

    /// <summary>
    /// Base URL for generating links in emails and redirects.
    /// Example: "https://lrm.cloud" or "http://localhost:3000"
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:3000";
}

/// <summary>
/// Database connection configuration.
/// </summary>
public sealed class DatabaseConfiguration
{
    /// <summary>
    /// PostgreSQL connection string.
    /// Example: "Host=lrmcloud-postgres;Port=5432;Database=lrmcloud;Username=lrm;Password=xxx"
    /// </summary>
    [Required]
    public required string ConnectionString { get; init; }

    /// <summary>
    /// Optional: Run migrations automatically on startup.
    /// Default: true for self-hosted deployments.
    /// </summary>
    public bool AutoMigrate { get; init; } = true;
}

/// <summary>
/// Redis connection configuration.
/// </summary>
public sealed class RedisConfiguration
{
    /// <summary>
    /// Redis connection string.
    /// Example: "lrmcloud-redis:6379,password=xxx"
    /// </summary>
    [Required]
    public required string ConnectionString { get; init; }
}

/// <summary>
/// Object storage configuration (MinIO / S3).
/// </summary>
public sealed class StorageConfiguration
{
    /// <summary>
    /// Storage endpoint (without protocol).
    /// Example: "lrmcloud-minio:9000" or "s3.amazonaws.com"
    /// </summary>
    [Required]
    public required string Endpoint { get; init; }

    /// <summary>
    /// Access key / username.
    /// </summary>
    [Required]
    public required string AccessKey { get; init; }

    /// <summary>
    /// Secret key / password.
    /// </summary>
    [Required]
    public required string SecretKey { get; init; }

    /// <summary>
    /// Bucket name for storing files.
    /// </summary>
    [Required]
    public required string Bucket { get; init; }

    /// <summary>
    /// Whether to use SSL/TLS for storage connections.
    /// Set to false for local MinIO, true for AWS S3.
    /// </summary>
    public bool UseSSL { get; init; } = false;

    /// <summary>
    /// Optional: AWS region for S3. Not needed for MinIO.
    /// </summary>
    public string? Region { get; init; }
}

/// <summary>
/// Encryption configuration for sensitive data.
/// </summary>
public sealed class EncryptionConfiguration
{
    /// <summary>
    /// Base64-encoded AES-256 key for encrypting tokens and sensitive data.
    /// Generate with: openssl rand -base64 32
    /// </summary>
    [Required]
    [MinLength(32)]
    public required string TokenKey { get; init; }
}

/// <summary>
/// Authentication configuration.
/// </summary>
public sealed class AuthConfiguration
{
    /// <summary>
    /// Secret key for signing JWT tokens.
    /// Should be at least 64 characters.
    /// </summary>
    [Required]
    [MinLength(32)]
    public required string JwtSecret { get; init; }

    /// <summary>
    /// JWT token expiry in hours.
    /// Default: 1 hour (short-lived when using refresh tokens).
    /// </summary>
    [Range(1, 720)]
    public int JwtExpiryHours { get; init; } = 1;

    /// <summary>
    /// Refresh token expiry in days.
    /// Default: 7 days.
    /// </summary>
    [Range(1, 90)]
    public int RefreshTokenExpiryDays { get; init; } = 7;

    /// <summary>
    /// Optional: GitHub OAuth App client ID.
    /// </summary>
    public string? GitHubClientId { get; init; }

    /// <summary>
    /// Optional: GitHub OAuth App client secret.
    /// </summary>
    public string? GitHubClientSecret { get; init; }

    /// <summary>
    /// Optional: GitHub App ID for repository integration.
    /// </summary>
    public string? GitHubAppId { get; init; }

    /// <summary>
    /// Optional: GitHub App private key (PEM format).
    /// </summary>
    public string? GitHubAppPrivateKey { get; init; }

    /// <summary>
    /// Optional: GitHub webhook secret for verifying payloads.
    /// </summary>
    public string? GitHubWebhookSecret { get; init; }

    /// <summary>
    /// Maximum failed login attempts before account lockout.
    /// Default: 5 attempts.
    /// </summary>
    [Range(1, 100)]
    public int MaxFailedLoginAttempts { get; init; } = 5;

    /// <summary>
    /// Account lockout duration in minutes.
    /// Default: 15 minutes.
    /// </summary>
    [Range(1, 1440)]
    public int LockoutDurationMinutes { get; init; } = 15;

    /// <summary>
    /// Email verification token expiry in hours.
    /// Default: 24 hours.
    /// </summary>
    [Range(1, 168)]
    public int EmailVerificationExpiryHours { get; init; } = 24;

    /// <summary>
    /// Password reset token expiry in hours.
    /// Default: 1 hour.
    /// </summary>
    [Range(1, 24)]
    public int PasswordResetExpiryHours { get; init; } = 1;

    /// <summary>
    /// Password requirements for registration.
    /// </summary>
    public PasswordRequirementsConfiguration PasswordRequirements { get; init; } = new();
}

/// <summary>
/// Password requirements configuration.
/// </summary>
public sealed class PasswordRequirementsConfiguration
{
    /// <summary>
    /// Minimum password length.
    /// Default: 12 characters.
    /// </summary>
    [Range(8, 128)]
    public int MinLength { get; init; } = 12;

    /// <summary>
    /// Require at least one uppercase letter.
    /// Default: true.
    /// </summary>
    public bool RequireUppercase { get; init; } = true;

    /// <summary>
    /// Require at least one lowercase letter.
    /// Default: true.
    /// </summary>
    public bool RequireLowercase { get; init; } = true;

    /// <summary>
    /// Require at least one digit.
    /// Default: true.
    /// </summary>
    public bool RequireDigit { get; init; } = true;

    /// <summary>
    /// Require at least one special character.
    /// Default: true.
    /// </summary>
    public bool RequireSpecialChar { get; init; } = true;
}

/// <summary>
/// Email configuration.
/// </summary>
public sealed class MailConfiguration
{
    /// <summary>
    /// SMTP server hostname.
    /// Example: "localhost", "smtp.mailgun.org"
    /// </summary>
    [Required]
    public required string Host { get; init; }

    /// <summary>
    /// SMTP server port.
    /// Common: 25 (unencrypted), 587 (STARTTLS), 465 (SSL/TLS)
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; init; } = 25;

    /// <summary>
    /// SMTP username (optional for local sendmail).
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// SMTP password (optional for local sendmail).
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Sender email address.
    /// Example: "noreply@lrm.cloud"
    /// </summary>
    [Required]
    [EmailAddress]
    public required string FromAddress { get; init; }

    /// <summary>
    /// Sender display name.
    /// Example: "LRM Cloud"
    /// </summary>
    [Required]
    public required string FromName { get; init; }

    /// <summary>
    /// Whether to use SSL/TLS for SMTP connection.
    /// </summary>
    public bool UseSsl { get; init; } = false;
}

/// <summary>
/// Feature flags.
/// </summary>
public sealed class FeaturesConfiguration
{
    /// <summary>
    /// Allow new user registration.
    /// Set to false for private instances.
    /// </summary>
    public bool Registration { get; init; } = true;

    /// <summary>
    /// Enable GitHub repository sync.
    /// </summary>
    public bool GitHubSync { get; init; } = true;

    /// <summary>
    /// Enable free translation tier.
    /// </summary>
    public bool FreeTranslations { get; init; } = true;

    /// <summary>
    /// Enable team/organization features.
    /// </summary>
    public bool Teams { get; init; } = true;
}

/// <summary>
/// Usage limits per tier.
/// </summary>
public sealed class LimitsConfiguration
{
    // ==========================================================================
    // Free Tier Limits
    // ==========================================================================

    /// <summary>
    /// Free tier: LRM translation character limit per month.
    /// Default: 10,000 characters.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int FreeTranslationChars { get; init; } = 10_000;

    /// <summary>
    /// Free tier: Other providers (BYOK + community) character limit per month.
    /// Default: 50,000 characters.
    /// </summary>
    [Range(0, long.MaxValue)]
    public long FreeOtherChars { get; init; } = 50_000;

    /// <summary>
    /// Free tier: Maximum projects.
    /// Default: 5 projects.
    /// </summary>
    [Range(1, 1000)]
    public int FreeMaxProjects { get; init; } = 5;

    /// <summary>
    /// Free tier: Maximum API keys (BYOK).
    /// Default: 3 keys.
    /// </summary>
    [Range(1, 100)]
    public int FreeMaxApiKeys { get; init; } = 3;

    // ==========================================================================
    // Team Tier Limits
    // ==========================================================================

    /// <summary>
    /// Team tier: LRM translation character limit per month.
    /// Default: 100,000 characters.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int TeamTranslationChars { get; init; } = 100_000;

    /// <summary>
    /// Team tier: Other providers (BYOK + community) character limit per month.
    /// Default: 500,000 characters.
    /// </summary>
    [Range(0, long.MaxValue)]
    public long TeamOtherChars { get; init; } = 500_000;

    /// <summary>
    /// Team tier: Maximum team members.
    /// Default: 20 members.
    /// </summary>
    [Range(1, 1000)]
    public int TeamMaxMembers { get; init; } = 20;

    /// <summary>
    /// Team tier: Maximum API keys (BYOK).
    /// Default: 10 keys.
    /// </summary>
    [Range(1, 100)]
    public int TeamMaxApiKeys { get; init; } = 10;

    // ==========================================================================
    // Enterprise Tier (unlimited, but configurable)
    // ==========================================================================

    /// <summary>
    /// Enterprise tier: LRM translation character limit per month.
    /// Default: 10M/month.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int EnterpriseTranslationChars { get; init; } = 10_000_000; // 10M/month

    /// <summary>
    /// Enterprise tier: Other providers character limit per month.
    /// Default: 50M/month.
    /// </summary>
    [Range(0, long.MaxValue)]
    public long EnterpriseOtherChars { get; init; } = 50_000_000; // 50M/month

    // ==========================================================================
    // General Limits (all tiers)
    // ==========================================================================

    /// <summary>
    /// Maximum keys per project.
    /// Default: 10,000 keys.
    /// </summary>
    [Range(1, 1_000_000)]
    public int MaxKeysPerProject { get; init; } = 10_000;

    /// <summary>
    /// Maximum file size for import (in bytes).
    /// Default: 10MB.
    /// </summary>
    [Range(1, 100_000_000)]
    public int MaxFileSize { get; init; } = 10_000_000;

    // Legacy properties for backward compatibility
    public int MaxProjectsPerUser => FreeMaxProjects;

    /// <summary>
    /// API rate limit (requests per minute per user).
    /// Default: 60 requests/minute.
    /// </summary>
    [Range(1, 10000)]
    public int ApiRateLimit { get; init; } = 60;

    // ==========================================================================
    // Helper Methods to get limits by plan
    // ==========================================================================

    /// <summary>
    /// Get LRM translation character limit for a given plan.
    /// </summary>
    public int GetTranslationCharsLimit(string plan) => plan?.ToLowerInvariant() switch
    {
        "team" => TeamTranslationChars,
        "enterprise" => EnterpriseTranslationChars,
        _ => FreeTranslationChars
    };

    /// <summary>
    /// Get other providers character limit for a given plan.
    /// </summary>
    public long GetOtherCharsLimit(string plan) => plan?.ToLowerInvariant() switch
    {
        "team" => TeamOtherChars,
        "enterprise" => EnterpriseOtherChars,
        _ => FreeOtherChars
    };

    /// <summary>
    /// Get maximum projects for a given plan.
    /// </summary>
    public int GetMaxProjects(string plan) => plan?.ToLowerInvariant() switch
    {
        "team" or "enterprise" => int.MaxValue, // Unlimited
        _ => FreeMaxProjects
    };

    /// <summary>
    /// Get maximum API keys for a given plan.
    /// </summary>
    public int GetMaxApiKeys(string plan) => plan?.ToLowerInvariant() switch
    {
        "team" => TeamMaxApiKeys,
        "enterprise" => int.MaxValue, // Unlimited
        _ => FreeMaxApiKeys
    };

    /// <summary>
    /// Get maximum team members for a given plan.
    /// </summary>
    public int GetMaxTeamMembers(string plan) => plan?.ToLowerInvariant() switch
    {
        "team" => TeamMaxMembers,
        "enterprise" => int.MaxValue, // Unlimited
        _ => 0 // Free tier has no team support
    };
}

/// <summary>
/// LRM managed translation provider configuration.
/// Controls which backends are used when users select "LRM Translation".
/// </summary>
public sealed class LrmProviderConfiguration
{
    /// <summary>
    /// Enabled backend providers for LRM, in priority order.
    /// First available provider will be used.
    /// Example: ["mymemory", "lingva", "deepl", "google"]
    /// </summary>
    public List<string> EnabledBackends { get; init; } = new() { "mymemory", "lingva" };

    /// <summary>
    /// Backend selection strategy.
    /// "priority" = Use first available in EnabledBackends order.
    /// "roundrobin" = Rotate between available backends.
    /// </summary>
    public string SelectionStrategy { get; init; } = "priority";

    /// <summary>
    /// Whether LRM provider is enabled.
    /// When disabled, users can only use BYOK.
    /// </summary>
    public bool Enabled { get; init; } = true;
}
