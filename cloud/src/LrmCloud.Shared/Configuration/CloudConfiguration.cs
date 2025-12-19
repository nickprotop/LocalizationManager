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

    /// <summary>
    /// Payment provider configuration.
    /// Controls which payment providers are enabled and active.
    /// </summary>
    public PaymentConfiguration? Payment { get; init; }

    /// <summary>
    /// Legacy Stripe configuration property.
    /// Use Payment.Stripe instead for new code.
    /// </summary>
    [Obsolete("Use Payment.Stripe instead")]
    public StripeConfiguration? Stripe => Payment?.Stripe;
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
    /// Example: "https://lrm-cloud.com" or "http://localhost:3000"
    /// </summary>
    public string BaseUrl { get; init; } = "http://localhost:3000";

    /// <summary>
    /// CORS configuration for API access.
    /// </summary>
    public CorsConfiguration? Cors { get; init; }
}

/// <summary>
/// CORS configuration for API access.
/// </summary>
public sealed class CorsConfiguration
{
    /// <summary>
    /// CORS mode: "allow-all" (allow any origin), "same-origin" (no CORS), "whitelist" (specific origins).
    /// Default: "allow-all".
    /// </summary>
    public string Mode { get; init; } = "allow-all";

    /// <summary>
    /// Allowed origins when mode is "whitelist".
    /// Example: ["https://app.lrm-cloud.com", "https://lrm-cloud.com"]
    /// </summary>
    public List<string> AllowedOrigins { get; init; } = new();

    /// <summary>
    /// Whether to allow credentials (cookies, auth headers).
    /// Only applies to "whitelist" mode.
    /// </summary>
    public bool AllowCredentials { get; init; } = false;
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
    /// Mail backend to use: "smtp" (default) or "imap".
    /// - smtp: Uses SMTP protocol for sending emails (traditional, supports local sendmail)
    /// - imap: Uses IMAP for connecting to an existing mail infrastructure (reads from drafts, moves to sent)
    /// </summary>
    public string Backend { get; init; } = "smtp";

    /// <summary>
    /// SMTP server hostname (for smtp backend).
    /// Example: "localhost", "smtp.mailgun.org"
    /// </summary>
    public string Host { get; init; } = "localhost";

    /// <summary>
    /// Server port.
    /// SMTP common: 25 (unencrypted), 587 (STARTTLS), 465 (SSL/TLS)
    /// IMAP common: 143 (unencrypted), 993 (SSL/TLS)
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; init; } = 25;

    /// <summary>
    /// Username for authentication (optional for local sendmail).
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Password for authentication (optional for local sendmail).
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Sender email address.
    /// Example: "noreply@lrm-cloud.com"
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
    /// Whether to use SSL/TLS for connection.
    /// </summary>
    public bool UseSsl { get; init; } = false;

    /// <summary>
    /// IMAP-specific configuration (only used when Backend = "imap").
    /// </summary>
    public ImapConfiguration? Imap { get; init; }
}

/// <summary>
/// IMAP-specific configuration for the imap mail backend.
/// This backend connects to an existing IMAP mail server and sends emails
/// by creating them in the Drafts folder, then sending via SMTP submission.
/// </summary>
public sealed class ImapConfiguration
{
    /// <summary>
    /// IMAP server hostname.
    /// Example: "imap.example.com"
    /// </summary>
    [Required]
    public required string Host { get; init; }

    /// <summary>
    /// IMAP server port (default: 993 for SSL, 143 for unencrypted).
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; init; } = 993;

    /// <summary>
    /// Whether to use SSL/TLS for IMAP connection.
    /// </summary>
    public bool UseSsl { get; init; } = true;

    /// <summary>
    /// SMTP submission server hostname (for sending via IMAP infrastructure).
    /// If not specified, uses the main mail Host.
    /// Example: "smtp.example.com"
    /// </summary>
    public string? SmtpHost { get; init; }

    /// <summary>
    /// SMTP submission port (default: 587 for STARTTLS, 465 for SSL).
    /// </summary>
    [Range(1, 65535)]
    public int SmtpPort { get; init; } = 587;

    /// <summary>
    /// Whether to use SSL/TLS for SMTP submission.
    /// </summary>
    public bool SmtpUseSsl { get; init; } = false;

    /// <summary>
    /// Folder to store sent emails (default: "Sent").
    /// The service will copy sent messages to this folder.
    /// </summary>
    public string SentFolder { get; init; } = "Sent";

    /// <summary>
    /// Whether to save sent emails to the Sent folder.
    /// </summary>
    public bool SaveToSent { get; init; } = true;
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
    /// Default: 5,000 characters.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int FreeTranslationChars { get; init; } = 5_000;

    /// <summary>
    /// Free tier: Other providers (BYOK + community) character limit per month.
    /// Default: 25,000 characters.
    /// </summary>
    [Range(0, long.MaxValue)]
    public long FreeOtherChars { get; init; } = 25_000;

    /// <summary>
    /// Free tier: Maximum projects.
    /// Default: 3 projects.
    /// </summary>
    [Range(1, 1000)]
    public int FreeMaxProjects { get; init; } = 3;

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
    /// Default: 50,000 characters.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int TeamTranslationChars { get; init; } = 50_000;

    /// <summary>
    /// Team tier: Other providers (BYOK + community) character limit per month.
    /// Default: 250,000 characters.
    /// </summary>
    [Range(0, long.MaxValue)]
    public long TeamOtherChars { get; init; } = 250_000;

    /// <summary>
    /// Team tier: Maximum team members.
    /// Default: 10 members.
    /// </summary>
    [Range(1, 1000)]
    public int TeamMaxMembers { get; init; } = 10;

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
    /// Default: 500K/month.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int EnterpriseTranslationChars { get; init; } = 500_000; // 500K/month

    /// <summary>
    /// Enterprise tier: Other providers character limit per month.
    /// Default: 2.5M/month.
    /// </summary>
    [Range(0, long.MaxValue)]
    public long EnterpriseOtherChars { get; init; } = 2_500_000; // 2.5M/month

    // ==========================================================================
    // Snapshot Limits per Plan (per project)
    // ==========================================================================

    /// <summary>
    /// Free tier: Maximum snapshots per project.
    /// Default: 3 snapshots.
    /// </summary>
    [Range(1, 100)]
    public int FreeMaxSnapshots { get; init; } = 3;

    /// <summary>
    /// Team tier: Maximum snapshots per project.
    /// Default: 10 snapshots.
    /// </summary>
    [Range(1, 100)]
    public int TeamMaxSnapshots { get; init; } = 10;

    /// <summary>
    /// Enterprise tier: Maximum snapshots per project.
    /// Default: 30 snapshots.
    /// </summary>
    [Range(1, 100)]
    public int EnterpriseMaxSnapshots { get; init; } = 30;

    // ==========================================================================
    // Snapshot Retention Days per Plan
    // ==========================================================================

    /// <summary>
    /// Free tier: Snapshot retention in days.
    /// Default: 7 days.
    /// </summary>
    [Range(1, 365)]
    public int FreeSnapshotRetentionDays { get; init; } = 7;

    /// <summary>
    /// Team tier: Snapshot retention in days.
    /// Default: 30 days.
    /// </summary>
    [Range(1, 365)]
    public int TeamSnapshotRetentionDays { get; init; } = 30;

    /// <summary>
    /// Enterprise tier: Snapshot retention in days.
    /// Default: 90 days.
    /// </summary>
    [Range(1, 365)]
    public int EnterpriseSnapshotRetentionDays { get; init; } = 90;

    // ==========================================================================
    // Storage Limits per Plan (bytes per ACCOUNT, not per project)
    // ==========================================================================

    /// <summary>
    /// Free tier: Maximum storage in bytes per account.
    /// Default: 25 MB.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long FreeMaxStorageBytes { get; init; } = 26_214_400; // 25 MB

    /// <summary>
    /// Team tier: Maximum storage in bytes per account.
    /// Default: 250 MB.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long TeamMaxStorageBytes { get; init; } = 262_144_000; // 250 MB

    /// <summary>
    /// Enterprise tier: Maximum storage in bytes per account.
    /// Default: 500 MB.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long EnterpriseMaxStorageBytes { get; init; } = 524_288_000; // 500 MB

    // ==========================================================================
    // File Size Limits per Plan (bytes)
    // ==========================================================================

    /// <summary>
    /// Free tier: Maximum file size in bytes.
    /// Default: 1 MB.
    /// </summary>
    [Range(1, 100_000_000)]
    public int FreeMaxFileSizeBytes { get; init; } = 1_048_576; // 1 MB

    /// <summary>
    /// Team tier: Maximum file size in bytes.
    /// Default: 2 MB.
    /// </summary>
    [Range(1, 100_000_000)]
    public int TeamMaxFileSizeBytes { get; init; } = 2_097_152; // 2 MB

    /// <summary>
    /// Enterprise tier: Maximum file size in bytes.
    /// Default: 5 MB.
    /// </summary>
    [Range(1, 100_000_000)]
    public int EnterpriseMaxFileSizeBytes { get; init; } = 5_242_880; // 5 MB

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

    /// <summary>
    /// Get maximum snapshots per project for a given plan.
    /// </summary>
    public int GetMaxSnapshots(string plan) => plan?.ToLowerInvariant() switch
    {
        "team" => TeamMaxSnapshots,
        "enterprise" => EnterpriseMaxSnapshots,
        _ => FreeMaxSnapshots
    };

    /// <summary>
    /// Get snapshot retention days for a given plan.
    /// </summary>
    public int GetSnapshotRetentionDays(string plan) => plan?.ToLowerInvariant() switch
    {
        "team" => TeamSnapshotRetentionDays,
        "enterprise" => EnterpriseSnapshotRetentionDays,
        _ => FreeSnapshotRetentionDays
    };

    /// <summary>
    /// Get maximum storage bytes per account for a given plan.
    /// </summary>
    public long GetMaxStorageBytes(string plan) => plan?.ToLowerInvariant() switch
    {
        "team" => TeamMaxStorageBytes,
        "enterprise" => EnterpriseMaxStorageBytes,
        _ => FreeMaxStorageBytes
    };

    /// <summary>
    /// Get maximum file size bytes for a given plan.
    /// </summary>
    public int GetMaxFileSizeBytes(string plan) => plan?.ToLowerInvariant() switch
    {
        "team" => TeamMaxFileSizeBytes,
        "enterprise" => EnterpriseMaxFileSizeBytes,
        _ => FreeMaxFileSizeBytes
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

    /// <summary>
    /// Backend-specific configuration (API keys, endpoints, models).
    /// </summary>
    public LrmBackendsConfiguration Backends { get; init; } = new();
}

/// <summary>
/// Configuration for all LRM backend providers.
/// </summary>
public sealed class LrmBackendsConfiguration
{
    public LrmMyMemoryConfig? MyMemory { get; init; }
    public LrmLingvaConfig? Lingva { get; init; }
    public LrmDeepLConfig? DeepL { get; init; }
    public LrmGoogleConfig? Google { get; init; }
    public LrmOpenAIConfig? OpenAI { get; init; }
    public LrmClaudeConfig? Claude { get; init; }
    public LrmAzureOpenAIConfig? AzureOpenAI { get; init; }
    public LrmAzureTranslatorConfig? AzureTranslator { get; init; }
    public LrmLibreTranslateConfig? LibreTranslate { get; init; }
    public LrmOllamaConfig? Ollama { get; init; }
}

/// <summary>
/// MyMemory backend configuration (free, no API key needed).
/// </summary>
public sealed class LrmMyMemoryConfig
{
    public int RateLimitPerMinute { get; init; } = 20;
}

/// <summary>
/// Lingva backend configuration (free Google Translate proxy).
/// </summary>
public sealed class LrmLingvaConfig
{
    public string InstanceUrl { get; init; } = "https://lingva.ml";
    public int RateLimitPerMinute { get; init; } = 30;
}

/// <summary>
/// DeepL backend configuration (requires API key).
/// </summary>
public sealed class LrmDeepLConfig
{
    public string? ApiKey { get; init; }
    public bool UseFreeApi { get; init; } = false;
    public int RateLimitPerMinute { get; init; } = 100;
}

/// <summary>
/// Google Cloud Translation backend configuration (requires API key).
/// </summary>
public sealed class LrmGoogleConfig
{
    public string? ApiKey { get; init; }
    public int RateLimitPerMinute { get; init; } = 100;
}

/// <summary>
/// OpenAI backend configuration (requires API key).
/// </summary>
public sealed class LrmOpenAIConfig
{
    public string? ApiKey { get; init; }
    public string Model { get; init; } = "gpt-4o-mini";
    public string? CustomSystemPrompt { get; init; }
    public int RateLimitPerMinute { get; init; } = 60;
}

/// <summary>
/// Claude (Anthropic) backend configuration (requires API key).
/// </summary>
public sealed class LrmClaudeConfig
{
    public string? ApiKey { get; init; }
    public string Model { get; init; } = "claude-3-5-sonnet-20241022";
    public string? CustomSystemPrompt { get; init; }
    public int RateLimitPerMinute { get; init; } = 50;
}

/// <summary>
/// Azure OpenAI backend configuration (requires API key and endpoint).
/// </summary>
public sealed class LrmAzureOpenAIConfig
{
    public string? ApiKey { get; init; }
    public string? Endpoint { get; init; }
    public string? DeploymentName { get; init; }
    public string? CustomSystemPrompt { get; init; }
    public int RateLimitPerMinute { get; init; } = 60;
}

/// <summary>
/// Azure Translator backend configuration (requires API key).
/// </summary>
public sealed class LrmAzureTranslatorConfig
{
    public string? ApiKey { get; init; }
    public string? Region { get; init; }
    public string? Endpoint { get; init; }
    public int RateLimitPerMinute { get; init; } = 100;
}

/// <summary>
/// LibreTranslate backend configuration (API key optional for public instances).
/// </summary>
public sealed class LrmLibreTranslateConfig
{
    public string? ApiKey { get; init; }
    public string InstanceUrl { get; init; } = "https://libretranslate.com";
    public int RateLimitPerMinute { get; init; } = 30;
}

/// <summary>
/// Ollama backend configuration (local LLM, no API key needed).
/// </summary>
public sealed class LrmOllamaConfig
{
    public string ApiUrl { get; init; } = "http://localhost:11434";
    public string Model { get; init; } = "llama3.2";
    public string? CustomSystemPrompt { get; init; }
    public int RateLimitPerMinute { get; init; } = 10;
}

/// <summary>
/// Stripe billing configuration.
/// Supports both test and live modes with separate key sets.
/// </summary>
public sealed class StripeConfiguration
{
    /// <summary>
    /// Current mode: "test" or "live".
    /// Determines which key set to use.
    /// Default: "test" for safety.
    /// </summary>
    public string Mode { get; init; } = "test";

    /// <summary>
    /// Test mode keys (sk_test_*, pk_test_*).
    /// Used when Mode = "test".
    /// </summary>
    public StripeKeySet Test { get; init; } = new();

    /// <summary>
    /// Live mode keys (sk_live_*, pk_live_*).
    /// Used when Mode = "live".
    /// </summary>
    public StripeKeySet Live { get; init; } = new();

    /// <summary>
    /// Stripe Price ID for Team plan ($9/month).
    /// Create in Stripe Dashboard under Products.
    /// </summary>
    public string TeamPriceId { get; init; } = "";

    /// <summary>
    /// Stripe Price ID for Enterprise plan ($29/month).
    /// Create in Stripe Dashboard under Products.
    /// </summary>
    public string EnterprisePriceId { get; init; } = "";

    /// <summary>
    /// Whether Stripe billing is enabled.
    /// When false, upgrade buttons are hidden.
    /// </summary>
    public bool Enabled { get; init; } = false;

    // ==========================================================================
    // Helper properties to get active keys based on mode
    // ==========================================================================

    /// <summary>
    /// Get the active secret key based on current mode.
    /// </summary>
    [JsonIgnore]
    public string SecretKey => Mode.ToLowerInvariant() == "live" ? Live.SecretKey : Test.SecretKey;

    /// <summary>
    /// Get the active publishable key based on current mode.
    /// </summary>
    [JsonIgnore]
    public string PublishableKey => Mode.ToLowerInvariant() == "live" ? Live.PublishableKey : Test.PublishableKey;

    /// <summary>
    /// Get the active webhook secret based on current mode.
    /// </summary>
    [JsonIgnore]
    public string WebhookSecret => Mode.ToLowerInvariant() == "live" ? Live.WebhookSecret : Test.WebhookSecret;

    /// <summary>
    /// Check if Stripe is properly configured (has required keys).
    /// </summary>
    [JsonIgnore]
    public bool IsConfigured => Enabled && !string.IsNullOrEmpty(SecretKey) && !string.IsNullOrEmpty(TeamPriceId);

    /// <summary>
    /// Check if currently in live mode.
    /// </summary>
    [JsonIgnore]
    public bool IsLiveMode => Mode.ToLowerInvariant() == "live";
}

/// <summary>
/// A set of Stripe API keys for a specific mode (test or live).
/// </summary>
public sealed class StripeKeySet
{
    /// <summary>
    /// Stripe secret key (sk_test_* or sk_live_*).
    /// Used for server-side API calls.
    /// </summary>
    public string SecretKey { get; init; } = "";

    /// <summary>
    /// Stripe publishable key (pk_test_* or pk_live_*).
    /// Used for client-side (Checkout, Elements).
    /// </summary>
    public string PublishableKey { get; init; } = "";

    /// <summary>
    /// Stripe webhook signing secret (whsec_*).
    /// Used to verify webhook payloads.
    /// </summary>
    public string WebhookSecret { get; init; } = "";
}

/// <summary>
/// Payment provider configuration.
/// Controls which payment providers are enabled and which one is active.
/// </summary>
public sealed class PaymentConfiguration
{
    /// <summary>
    /// The active payment provider to use for new subscriptions.
    /// Options: "stripe", "paypal", "none".
    /// Default: "stripe".
    /// </summary>
    public string ActiveProvider { get; init; } = "stripe";

    /// <summary>
    /// Stripe configuration.
    /// Optional - Stripe disabled if not configured.
    /// </summary>
    public StripeConfiguration? Stripe { get; init; }

    /// <summary>
    /// PayPal configuration.
    /// Optional - PayPal disabled if not configured.
    /// </summary>
    public PayPalConfiguration? PayPal { get; init; }

    /// <summary>
    /// Check if a specific provider is enabled.
    /// Uses the provider's own Enabled field.
    /// </summary>
    public bool IsProviderEnabled(string providerName)
    {
        return providerName?.ToLowerInvariant() switch
        {
            "stripe" => Stripe?.Enabled == true,
            "paypal" => PayPal?.Enabled == true,
            _ => false
        };
    }

    /// <summary>
    /// Check if any payment provider is enabled.
    /// </summary>
    [JsonIgnore]
    public bool HasEnabledProvider => (Stripe?.Enabled == true) || (PayPal?.Enabled == true);

    /// <summary>
    /// Get the active provider if it's enabled, otherwise null.
    /// </summary>
    [JsonIgnore]
    public string? ActiveEnabledProvider =>
        IsProviderEnabled(ActiveProvider) ? ActiveProvider : null;
}

/// <summary>
/// PayPal billing configuration.
/// Supports both sandbox and live modes.
/// </summary>
public sealed class PayPalConfiguration
{
    /// <summary>
    /// Current mode: "sandbox" or "live".
    /// Default: "sandbox" for safety.
    /// </summary>
    public string Mode { get; init; } = "sandbox";

    /// <summary>
    /// Sandbox mode credentials.
    /// Used when Mode = "sandbox".
    /// </summary>
    public PayPalKeySet Sandbox { get; init; } = new();

    /// <summary>
    /// Live mode credentials.
    /// Used when Mode = "live".
    /// </summary>
    public PayPalKeySet Live { get; init; } = new();

    /// <summary>
    /// PayPal Plan ID for Team plan ($9/month).
    /// Create in PayPal Dashboard under Subscriptions > Plans.
    /// </summary>
    public string TeamPlanId { get; init; } = "";

    /// <summary>
    /// PayPal Plan ID for Enterprise plan ($29/month).
    /// Create in PayPal Dashboard under Subscriptions > Plans.
    /// </summary>
    public string EnterprisePlanId { get; init; } = "";

    /// <summary>
    /// Whether PayPal billing is enabled.
    /// </summary>
    public bool Enabled { get; init; } = false;

    // ==========================================================================
    // Helper properties to get active credentials based on mode
    // ==========================================================================

    /// <summary>
    /// Get the active client ID based on current mode.
    /// </summary>
    [JsonIgnore]
    public string ClientId => Mode.ToLowerInvariant() == "live" ? Live.ClientId : Sandbox.ClientId;

    /// <summary>
    /// Get the active client secret based on current mode.
    /// </summary>
    [JsonIgnore]
    public string ClientSecret => Mode.ToLowerInvariant() == "live" ? Live.ClientSecret : Sandbox.ClientSecret;

    /// <summary>
    /// Get the active webhook ID based on current mode.
    /// </summary>
    [JsonIgnore]
    public string WebhookId => Mode.ToLowerInvariant() == "live" ? Live.WebhookId : Sandbox.WebhookId;

    /// <summary>
    /// Check if PayPal is properly configured (has required credentials).
    /// </summary>
    [JsonIgnore]
    public bool IsConfigured => Enabled && !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(ClientSecret);

    /// <summary>
    /// Check if currently in live mode.
    /// </summary>
    [JsonIgnore]
    public bool IsLiveMode => Mode.ToLowerInvariant() == "live";

    /// <summary>
    /// Get the PayPal API base URL for the current mode.
    /// </summary>
    [JsonIgnore]
    public string ApiBaseUrl => IsLiveMode
        ? "https://api-m.paypal.com"
        : "https://api-m.sandbox.paypal.com";
}

/// <summary>
/// A set of PayPal API credentials for a specific mode (sandbox or live).
/// </summary>
public sealed class PayPalKeySet
{
    /// <summary>
    /// PayPal Client ID.
    /// Get from PayPal Developer Dashboard > My Apps.
    /// </summary>
    public string ClientId { get; init; } = "";

    /// <summary>
    /// PayPal Client Secret.
    /// Get from PayPal Developer Dashboard > My Apps.
    /// </summary>
    public string ClientSecret { get; init; } = "";

    /// <summary>
    /// PayPal Webhook ID for signature verification.
    /// Get from PayPal Developer Dashboard > Webhooks.
    /// </summary>
    public string WebhookId { get; init; } = "";
}
