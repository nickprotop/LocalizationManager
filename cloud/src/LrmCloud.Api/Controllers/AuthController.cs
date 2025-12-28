using LrmCloud.Api.Helpers;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace LrmCloud.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAuthService _authService;
    private readonly IGitHubAuthService _githubAuthService;
    private readonly CloudConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IGitHubAuthService githubAuthService,
        CloudConfiguration config,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _githubAuthService = githubAuthService;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Get available authentication providers on this server.
    /// This endpoint is public (no auth required) to show/hide GitHub button on login page.
    /// </summary>
    [HttpGet("providers")]
    [DisableRateLimiting]
    [ProducesResponseType(typeof(ApiResponse<AuthProvidersDto>), StatusCodes.Status200OK)]
    public ActionResult<ApiResponse<AuthProvidersDto>> GetProviders()
    {
        return Success(new AuthProvidersDto
        {
            GitHub = !string.IsNullOrEmpty(_config.Auth?.GitHubClientId)
        });
    }

    /// <summary>
    /// Register a new user with email and password
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse>> Register([FromBody] RegisterRequest request)
    {
        // Additional password validation beyond data annotations
        var (isValid, errorMessage) = PasswordValidator.Validate(request.Password);
        if (!isValid)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);

        try
        {
            await _authService.RegisterAsync(request);

            // SECURITY: Always return the same message (prevents account enumeration)
            return Success("Registration successful. Please check your email to verify your account.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for email: {Email}", request.Email);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                Problem(
                    title: "Internal Server Error",
                    detail: "An error occurred during registration. Please try again later.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    type: $"https://lrm-cloud.com/errors/{ErrorCodes.SRV_INTERNAL_ERROR.ToLowerInvariant().Replace('_', '-')}"
                )
            );
        }
    }

    /// <summary>
    /// Verify email address with token from verification email
    /// </summary>
    [HttpPost("verify-email")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> VerifyEmail(
        [FromQuery] string email,
        [FromQuery] string token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
            return BadRequest(ErrorCodes.VAL_REQUIRED_FIELD, "Email and token are required");

        var (success, errorMessage) = await _authService.VerifyEmailAsync(email, token);

        if (!success)
            return BadRequest(ErrorCodes.AUTH_TOKEN_INVALID, errorMessage!);

        return Success("Email verified successfully. You can now log in.");
    }

    /// <summary>
    /// Request a password reset email
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ApiResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        try
        {
            await _authService.ForgotPasswordAsync(request);

            // SECURITY: Always return the same message (prevents account enumeration)
            return Success("If an account with that email exists, a password reset link has been sent.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during password reset request for email: {Email}", request.Email);
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                Problem(
                    title: "Internal Server Error",
                    detail: "An error occurred processing your request. Please try again later.",
                    statusCode: StatusCodes.Status500InternalServerError,
                    type: $"https://lrm-cloud.com/errors/{ErrorCodes.SRV_INTERNAL_ERROR.ToLowerInvariant().Replace('_', '-')}"
                )
            );
        }
    }

    /// <summary>
    /// Reset password with token from email
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        // Additional password validation beyond data annotations
        var (isValid, errorMessage) = PasswordValidator.Validate(request.NewPassword);
        if (!isValid)
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);

        var (success, error) = await _authService.ResetPasswordAsync(request);

        if (!success)
            return BadRequest(ErrorCodes.AUTH_TOKEN_INVALID, error!);

        return Success("Password reset successfully. You can now log in with your new password.");
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, response, errorMessage) = await _authService.LoginAsync(request, ipAddress);

        if (!success)
        {
            // Return 401 Unauthorized for invalid credentials
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: errorMessage!,
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_INVALID_CREDENTIALS.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        return Success(response!);
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh([FromBody] RefreshTokenRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var (success, response, errorMessage) = await _authService.RefreshTokenAsync(request.RefreshToken, ipAddress);

        if (!success)
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: errorMessage!,
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        return Success(response!);
    }

    /// <summary>
    /// Revoke refresh token (logout)
    /// </summary>
    [HttpPost("revoke")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse>> Revoke([FromBody] RefreshTokenRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var success = await _authService.RevokeRefreshTokenAsync(request.RefreshToken, ipAddress);

        if (!success)
            return BadRequest(ErrorCodes.AUTH_TOKEN_INVALID, "Invalid refresh token");

        return Success("Refresh token revoked successfully");
    }

    /// <summary>
    /// Get current authenticated user profile
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> GetCurrentUser()
    {
        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "Invalid authentication token",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        var userProfile = await _authService.GetCurrentUserAsync(userId);

        if (userProfile == null)
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "User not found",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        return Success(userProfile);
    }

    /// <summary>
    /// Update current user's profile
    /// </summary>
    [HttpPatch("profile")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "Invalid authentication token",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        var (success, profile, errorMessage) = await _authService.UpdateProfileAsync(userId, request);

        if (!success)
        {
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);
        }

        return Success(profile!);
    }

    /// <summary>
    /// Change password for authenticated user
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "Invalid authentication token",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        var (success, errorMessage) = await _authService.ChangePasswordAsync(userId, request);

        if (!success)
        {
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);
        }

        return Success("Password changed successfully. All sessions have been logged out for security.");
    }

    // ============================================================================
    // GitHub OAuth Endpoints
    // ============================================================================

    /// <summary>
    /// Initiate GitHub OAuth flow - redirects to GitHub authorization page
    /// </summary>
    [HttpGet("github")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult GitHubLogin()
    {
        try
        {
            var (authorizationUrl, state) = _githubAuthService.GetAuthorizationUrl();

            // Store state in cookie for CSRF validation
            Response.Cookies.Append("github_oauth_state", state, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10)
            });

            return Redirect(authorizationUrl);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "GitHub OAuth not configured");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                Problem(
                    title: "Service Unavailable",
                    detail: "GitHub OAuth is not configured on this server",
                    statusCode: StatusCodes.Status500InternalServerError,
                    type: $"https://lrm-cloud.com/errors/{ErrorCodes.SRV_INTERNAL_ERROR.ToLowerInvariant().Replace('_', '-')}"
                )
            );
        }
    }

    /// <summary>
    /// GitHub OAuth callback - handles authorization code exchange
    /// </summary>
    [HttpGet("github/callback")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GitHubCallback(
        [FromQuery] string code,
        [FromQuery] string state)
    {
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return BadRequest(ErrorCodes.VAL_REQUIRED_FIELD, "Missing code or state parameter");

        // Retrieve expected state from cookie
        if (!Request.Cookies.TryGetValue("github_oauth_state", out var expectedState))
        {
            return BadRequest(ErrorCodes.AUTH_TOKEN_INVALID, "OAuth state cookie not found. Please try again.");
        }

        // Delete state cookie
        Response.Cookies.Delete("github_oauth_state");

        // Check if this is a link operation (state format: "link:{userId}:{random}")
        var isLinkOperation = expectedState.StartsWith("link:");

        try
        {
            var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (success, response, errorMessage) = await _githubAuthService.HandleCallbackAsync(
                code, state, expectedState, ipAddress);

            if (!success)
            {
                if (isLinkOperation)
                {
                    // Redirect to profile with error
                    return Redirect($"{_config.Server.AppBaseUrl}/settings/profile?github_error={Uri.EscapeDataString(errorMessage ?? "Failed to link GitHub account")}");
                }
                return BadRequest(ErrorCodes.AUTH_INVALID_CREDENTIALS, errorMessage!);
            }

            if (isLinkOperation)
            {
                // Redirect to profile page with success indicator
                return Redirect($"{_config.Server.AppBaseUrl}/settings/profile?github_linked=true");
            }

            // Login operation - redirect to frontend with tokens
            // Using URL fragment (#) so tokens aren't logged in server access logs
            var redirectUrl = $"{_config.Server.AppBaseUrl}/auth/github/callback" +
                $"#token={Uri.EscapeDataString(response!.Token)}" +
                $"&expiresAt={Uri.EscapeDataString(response.ExpiresAt.ToString("O"))}" +
                $"&refreshToken={Uri.EscapeDataString(response.RefreshToken)}" +
                $"&refreshTokenExpiresAt={Uri.EscapeDataString(response.RefreshTokenExpiresAt.ToString("O"))}";

            return Redirect(redirectUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during GitHub OAuth callback");

            if (isLinkOperation)
            {
                return Redirect($"{_config.Server.AppBaseUrl}/settings/profile?github_error={Uri.EscapeDataString("An error occurred during GitHub authentication")}");
            }

            return StatusCode(
                StatusCodes.Status500InternalServerError,
                Problem(
                    title: "Internal Server Error",
                    detail: "An error occurred during GitHub authentication",
                    statusCode: StatusCodes.Status500InternalServerError,
                    type: $"https://lrm-cloud.com/errors/{ErrorCodes.SRV_INTERNAL_ERROR.ToLowerInvariant().Replace('_', '-')}"
                )
            );
        }
    }

    /// <summary>
    /// Unlink GitHub account from current user
    /// </summary>
    [HttpPost("github/unlink")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> UnlinkGitHub([FromBody] UnlinkGitHubRequest? request)
    {
        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "Invalid authentication token",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        var password = request?.Password;
        var (success, errorMessage) = await _githubAuthService.UnlinkGitHubAccountAsync(userId, password);

        if (!success)
        {
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);
        }

        return Success("GitHub account unlinked successfully");
    }

    // In-memory store for GitHub link codes (short-lived, 5 minutes)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int UserId, DateTime ExpiresAt)> _linkCodes = new();

    /// <summary>
    /// Initiate GitHub account linking - generates a short-lived code for the redirect.
    /// Call this first, then redirect to /api/auth/github/link?code={code}
    /// </summary>
    [HttpPost("github/link/initiate")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<GitHubLinkInitiateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public ActionResult<ApiResponse<GitHubLinkInitiateResponse>> InitiateGitHubLink()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "Invalid authentication token",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        // Clean up expired codes
        var expiredCodes = _linkCodes.Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow).Select(kvp => kvp.Key).ToList();
        foreach (var code in expiredCodes)
        {
            _linkCodes.TryRemove(code, out _);
        }

        // Generate a short-lived code
        var linkCode = Guid.NewGuid().ToString("N");
        _linkCodes[linkCode] = (userId, DateTime.UtcNow.AddMinutes(5));

        return Success(new GitHubLinkInitiateResponse(linkCode));
    }

    /// <summary>
    /// Link GitHub account to current user - initiates OAuth flow.
    /// For email users who want to add GitHub login capability.
    /// Requires a link code from /api/auth/github/link/initiate
    /// </summary>
    [HttpGet("github/link")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult LinkGitHub([FromQuery] string? code)
    {
        int userId;

        // Try to get user ID from link code (primary method for browser redirects)
        if (!string.IsNullOrEmpty(code) && _linkCodes.TryRemove(code, out var linkData))
        {
            if (linkData.ExpiresAt < DateTime.UtcNow)
            {
                return BadRequest(Problem(
                    title: "Link Expired",
                    detail: "The GitHub link code has expired. Please try again.",
                    statusCode: StatusCodes.Status400BadRequest,
                    type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_EXPIRED.ToLowerInvariant().Replace('_', '-')}"
                ));
            }
            userId = linkData.UserId;
        }
        // Fallback: Try JWT auth (for direct API calls)
        else if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out userId))
            {
                return Unauthorized(Problem(
                    title: "Unauthorized",
                    detail: "Invalid authentication token",
                    statusCode: StatusCodes.Status401Unauthorized,
                    type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
                ));
            }
        }
        else
        {
            return BadRequest(Problem(
                title: "Missing Link Code",
                detail: "A valid link code is required. Please initiate the linking process from your profile.",
                statusCode: StatusCodes.Status400BadRequest,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.VAL_REQUIRED_FIELD.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        try
        {
            // Generate authorization URL with link prefix in state
            var (authorizationUrl, state) = _githubAuthService.GetAuthorizationUrl($"link:{userId}");

            // Store state in cookie for CSRF validation
            Response.Cookies.Append("github_oauth_state", state, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromMinutes(10)
            });

            return Redirect(authorizationUrl);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "GitHub OAuth not configured");
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                Problem(
                    title: "Service Unavailable",
                    detail: "GitHub OAuth is not configured on this server",
                    statusCode: StatusCodes.Status500InternalServerError,
                    type: $"https://lrm-cloud.com/errors/{ErrorCodes.SRV_INTERNAL_ERROR.ToLowerInvariant().Replace('_', '-')}"
                )
            );
        }
    }

    // ============================================================================
    // Change Email Endpoints
    // ============================================================================

    /// <summary>
    /// Request to change email address - sends verification to new email
    /// </summary>
    [HttpPost("change-email")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> ChangeEmail([FromBody] ChangeEmailRequest request)
    {
        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "Invalid authentication token",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        var (success, errorMessage) = await _authService.ChangeEmailAsync(userId, request);

        if (!success)
        {
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);
        }

        return Success("Verification email sent to new address. Please check your email to confirm the change.");
    }

    /// <summary>
    /// Verify new email address with token from verification email
    /// </summary>
    [HttpPost("verify-new-email")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> VerifyNewEmail([FromBody] VerifyNewEmailRequest request)
    {
        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "Invalid authentication token",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        var (success, errorMessage) = await _authService.VerifyNewEmailAsync(userId, request.Token);

        if (!success)
        {
            return BadRequest(ErrorCodes.AUTH_TOKEN_INVALID, errorMessage!);
        }

        return Success("Email address changed successfully. You can now log in with your new email.");
    }

    // ============================================================================
    // Session Management Endpoints
    // ============================================================================

    /// <summary>
    /// Get all active sessions (refresh tokens) for current user
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<SessionDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<List<SessionDto>>>> GetSessions()
    {
        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "Invalid authentication token",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        // Try to get current refresh token from request header (optional)
        string? currentRefreshToken = null;
        if (Request.Headers.TryGetValue("X-Refresh-Token", out var refreshTokenHeader))
        {
            currentRefreshToken = refreshTokenHeader.ToString();
        }

        var sessions = await _authService.GetSessionsAsync(userId, currentRefreshToken);

        return Success(sessions);
    }

    /// <summary>
    /// Revoke a specific session (logout from that device)
    /// </summary>
    [HttpDelete("sessions/{sessionId}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> RevokeSession(int sessionId)
    {
        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "Invalid authentication token",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        var (success, errorMessage) = await _authService.RevokeSessionAsync(userId, sessionId);

        if (!success)
        {
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);
        }

        return Success("Session revoked successfully");
    }

    /// <summary>
    /// Revoke all other sessions except the current one (logout from all other devices)
    /// </summary>
    [HttpDelete("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> RevokeAllOtherSessions()
    {
        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "Invalid authentication token",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        // Get current refresh token from request header
        if (!Request.Headers.TryGetValue("X-Refresh-Token", out var refreshTokenHeader) ||
            string.IsNullOrEmpty(refreshTokenHeader.ToString()))
        {
            return BadRequest(ErrorCodes.VAL_REQUIRED_FIELD, "X-Refresh-Token header is required to revoke other sessions");
        }

        var currentRefreshToken = refreshTokenHeader.ToString();
        var (success, revokedCount, errorMessage) = await _authService.RevokeAllOtherSessionsAsync(userId, currentRefreshToken);

        if (!success)
        {
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);
        }

        return Success($"Successfully revoked {revokedCount} other session(s)");
    }

    /// <summary>
    /// Delete account (soft delete with 30-day grace period)
    /// </summary>
    [HttpPost("delete-account")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> DeleteAccount([FromBody] DeleteAccountRequest request)
    {
        // Extract user ID from JWT claims
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(Problem(
                title: "Unauthorized",
                detail: "Invalid authentication token",
                statusCode: StatusCodes.Status401Unauthorized,
                type: $"https://lrm-cloud.com/errors/{ErrorCodes.AUTH_TOKEN_INVALID.ToLowerInvariant().Replace('_', '-')}"
            ));
        }

        var (success, errorMessage) = await _authService.DeleteAccountAsync(userId, request);

        if (!success)
        {
            return BadRequest(ErrorCodes.VAL_INVALID_INPUT, errorMessage!);
        }

        return Success("Your account has been deleted successfully. All sessions have been logged out and you will receive a confirmation email.");
    }
}
