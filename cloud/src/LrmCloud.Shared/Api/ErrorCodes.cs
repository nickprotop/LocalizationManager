namespace LrmCloud.Shared.Api;

/// <summary>
/// Standardized error codes for ProblemDetails responses.
/// Use SCREAMING_SNAKE_CASE for consistency.
/// </summary>
public static class ErrorCodes
{
    // ==========================================================================
    // Authentication & Authorization (AUTH_*)
    // ==========================================================================
    public const string AUTH_INVALID_CREDENTIALS = "AUTH_INVALID_CREDENTIALS";
    public const string AUTH_EMAIL_NOT_VERIFIED = "AUTH_EMAIL_NOT_VERIFIED";
    public const string AUTH_ACCOUNT_LOCKED = "AUTH_ACCOUNT_LOCKED";
    public const string AUTH_TOKEN_EXPIRED = "AUTH_TOKEN_EXPIRED";
    public const string AUTH_TOKEN_INVALID = "AUTH_TOKEN_INVALID";
    public const string AUTH_UNAUTHORIZED = "AUTH_UNAUTHORIZED";
    public const string AUTH_FORBIDDEN = "AUTH_FORBIDDEN";

    // ==========================================================================
    // Registration (REG_*)
    // ==========================================================================
    public const string REG_EMAIL_EXISTS = "REG_EMAIL_EXISTS";
    public const string REG_DISABLED = "REG_DISABLED";
    public const string REG_INVALID_TOKEN = "REG_INVALID_TOKEN";
    public const string REG_TOKEN_EXPIRED = "REG_TOKEN_EXPIRED";

    // ==========================================================================
    // Validation (VAL_*)
    // ==========================================================================
    public const string VAL_INVALID_INPUT = "VAL_INVALID_INPUT";
    public const string VAL_REQUIRED_FIELD = "VAL_REQUIRED_FIELD";
    public const string VAL_INVALID_FORMAT = "VAL_INVALID_FORMAT";

    // ==========================================================================
    // Resources (RES_*)
    // ==========================================================================
    public const string RES_NOT_FOUND = "RES_NOT_FOUND";
    public const string RES_ALREADY_EXISTS = "RES_ALREADY_EXISTS";
    public const string RES_CONFLICT = "RES_CONFLICT";
    public const string RES_VERSION_MISMATCH = "RES_VERSION_MISMATCH";

    // ==========================================================================
    // Server (SRV_*)
    // ==========================================================================
    public const string SRV_INTERNAL_ERROR = "SRV_INTERNAL_ERROR";
    public const string SRV_SERVICE_UNAVAILABLE = "SRV_SERVICE_UNAVAILABLE";
    public const string SRV_RATE_LIMITED = "SRV_RATE_LIMITED";

    // ==========================================================================
    // External Services (EXT_*)
    // ==========================================================================
    public const string EXT_GITHUB_ERROR = "EXT_GITHUB_ERROR";
    public const string EXT_TRANSLATION_ERROR = "EXT_TRANSLATION_ERROR";
    public const string EXT_MAIL_ERROR = "EXT_MAIL_ERROR";

    // ==========================================================================
    // Translation (TRN_*)
    // ==========================================================================
    public const string TRN_KEY_NOT_FOUND = "TRN_KEY_NOT_FOUND";
    public const string TRN_CONFIG_NOT_FOUND = "TRN_CONFIG_NOT_FOUND";
    public const string TRN_PROVIDER_ERROR = "TRN_PROVIDER_ERROR";
    public const string TRN_SAVE_FAILED = "TRN_SAVE_FAILED";
    public const string TRN_INVALID_PROVIDER = "TRN_INVALID_PROVIDER";
}
