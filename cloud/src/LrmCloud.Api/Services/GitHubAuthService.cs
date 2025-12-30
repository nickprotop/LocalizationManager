using System.Text.Json;
using LrmCloud.Api.Data;
using LrmCloud.Api.Helpers;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Auth;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.Services;

public class GitHubAuthService : IGitHubAuthService
{
    private readonly AppDbContext _db;
    private readonly CloudConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubAuthService> _logger;

    private const string GitHubAuthorizeUrl = "https://github.com/login/oauth/authorize";
    private const string GitHubTokenUrl = "https://github.com/login/oauth/access_token";
    private const string GitHubUserApiUrl = "https://api.github.com/user";
    private const string GitHubEmailsApiUrl = "https://api.github.com/user/emails";

    public GitHubAuthService(
        AppDbContext db,
        CloudConfiguration config,
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubAuthService> logger)
    {
        _db = db;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public (string AuthorizationUrl, string State) GetAuthorizationUrl(string? statePrefix = null)
    {
        if (string.IsNullOrEmpty(_config.Auth.GitHubClientId))
            throw new InvalidOperationException("GitHub OAuth is not configured (GitHubClientId missing)");

        // Generate secure random state for CSRF protection
        var randomPart = TokenGenerator.GenerateSecureToken(32);

        // Include prefix if provided (e.g., "link:{userId}:{random}" for account linking)
        var state = string.IsNullOrEmpty(statePrefix)
            ? randomPart
            : $"{statePrefix}:{randomPart}";

        var callbackUrl = $"{_config.Server.BaseUrl}/api/auth/github/callback";
        // Request both user:email (for profile) and repo (for repository access) scopes
        var url = $"{GitHubAuthorizeUrl}?" +
                  $"client_id={Uri.EscapeDataString(_config.Auth.GitHubClientId)}&" +
                  $"redirect_uri={Uri.EscapeDataString(callbackUrl)}&" +
                  $"scope=user:email%20repo&" +
                  $"state={Uri.EscapeDataString(state)}";

        return (url, state);
    }

    public async Task<(bool Success, LoginResponse? Response, string? ErrorMessage)> HandleCallbackAsync(
        string code,
        string state,
        string expectedState,
        string? ipAddress = null)
    {
        // Validate state parameter (CSRF protection)
        if (state != expectedState)
        {
            _logger.LogWarning("GitHub OAuth state mismatch. Possible CSRF attack.");
            return (false, null, "Invalid OAuth state parameter");
        }

        // Check if this is a link operation (state format: "link:{userId}:{random}")
        int? linkUserId = null;
        if (expectedState.StartsWith("link:"))
        {
            var parts = expectedState.Split(':');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var userId))
            {
                linkUserId = userId;
            }
            else
            {
                _logger.LogWarning("Invalid link state format: {State}", expectedState);
                return (false, null, "Invalid link state format");
            }
        }

        try
        {
            // Exchange code for access token
            var accessToken = await ExchangeCodeForTokenAsync(code);
            if (accessToken == null)
                return (false, null, "Failed to obtain access token from GitHub");

            // Fetch user profile from GitHub
            var githubProfile = await FetchGitHubProfileAsync(accessToken);
            if (githubProfile == null)
                return (false, null, "Failed to fetch user profile from GitHub");

            User user;
            bool isNewUser;
            string? authEvent = null;
            string? relatedEmail = null;

            if (linkUserId.HasValue)
            {
                // This is a link operation - link GitHub to existing user
                var (success, linkedUser, error) = await LinkGitHubToExistingUserAsync(
                    linkUserId.Value, githubProfile, accessToken);

                if (!success)
                    return (false, null, error);

                user = linkedUser!;
                isNewUser = false;
            }
            else
            {
                // Normal login/register flow
                var createResult = await CreateOrLinkUserAsync(githubProfile, accessToken);
                user = createResult.User;
                isNewUser = createResult.IsNewUser;
                authEvent = createResult.AuthEvent;
                relatedEmail = createResult.RelatedEmail;
            }

            // Generate JWT and refresh tokens
            var (token, expiresAt) = JwtTokenGenerator.GenerateToken(
                user,
                _config.Auth.JwtSecret,
                _config.Auth.JwtExpiryHours);

            var (refreshToken, refreshTokenExpiresAt) = await GenerateRefreshTokenAsync(user.Id, ipAddress);

            // Update last login and sync limits
            user.LastLoginAt = DateTime.UtcNow;
            SyncUserLimitsWithConfig(user);
            await _db.SaveChangesAsync();

            var response = new LoginResponse
            {
                User = new UserDto
                {
                    Id = user.Id,
                    Email = user.Email!, // Email is always set (either from GitHub or generated)
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    AvatarUrl = user.AvatarUrl,
                    EmailVerified = user.EmailVerified,
                    Plan = user.Plan,
                    IsSuperAdmin = user.IsSuperAdmin,
                    CreatedAt = user.CreatedAt
                },
                Token = token,
                ExpiresAt = expiresAt,
                RefreshToken = refreshToken,
                RefreshTokenExpiresAt = refreshTokenExpiresAt,
                AuthEvent = authEvent,
                RelatedEmail = relatedEmail
            };

            _logger.LogInformation("GitHub OAuth successful for user {UserId} (GitHub ID: {GitHubId}). New user: {IsNew}",
                user.Id, user.GitHubId, isNewUser);

            return (true, response, null);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during GitHub OAuth flow");
            return (false, null, "Failed to communicate with GitHub");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during GitHub OAuth callback");
            return (false, null, "An error occurred during GitHub authentication");
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> UnlinkGitHubAccountAsync(int userId, string? password = null)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return (false, "User not found");

        // Check if user has GitHub linked
        if (user.AuthType != "github" && user.GitHubId == null)
            return (false, "No GitHub account linked");

        // If user only has GitHub auth and no password, they cannot unlink
        if (user.AuthType == "github" && string.IsNullOrEmpty(user.PasswordHash))
        {
            return (false, "Cannot unlink GitHub account without setting up email/password authentication first");
        }

        // If user has email/password auth, require password verification
        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            if (string.IsNullOrEmpty(password))
                return (false, "Password is required to unlink GitHub account");

            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return (false, "Incorrect password");
        }

        // Unlink GitHub account
        user.GitHubId = null;
        user.GitHubAccessTokenEncrypted = null;
        user.GitHubTokenExpiresAt = null;

        // If primary auth was GitHub, switch to email
        if (user.AuthType == "github")
            user.AuthType = "email";

        await _db.SaveChangesAsync();

        _logger.LogInformation("GitHub account unlinked for user {UserId}", userId);
        return (true, null);
    }

    /// <summary>
    /// Links a GitHub account to an existing user (for email users adding GitHub).
    /// </summary>
    private async Task<(bool Success, User? User, string? ErrorMessage)> LinkGitHubToExistingUserAsync(
        int userId,
        GitHubUserProfile githubProfile,
        string accessToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return (false, null, "User not found");

        // Check if this GitHub account is already linked to another user
        var existingGitHubUser = await _db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubProfile.Id);
        if (existingGitHubUser != null)
        {
            if (existingGitHubUser.Id == userId)
            {
                // Already linked to this user - just update token
                user.GitHubAccessTokenEncrypted = EncryptToken(accessToken);
                user.GitHubTokenExpiresAt = null;
                user.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                _logger.LogInformation("Updated GitHub token for user {UserId}", userId);
                return (true, user, null);
            }

            return (false, null, "This GitHub account is already linked to another user");
        }

        // Check if user already has a different GitHub account linked
        if (user.GitHubId != null && user.GitHubId != githubProfile.Id)
        {
            return (false, null, "A different GitHub account is already linked. Unlink it first.");
        }

        // Link GitHub account
        user.GitHubId = githubProfile.Id;
        user.GitHubAccessTokenEncrypted = EncryptToken(accessToken);
        user.GitHubTokenExpiresAt = null;
        user.AvatarUrl = githubProfile.AvatarUrl ?? user.AvatarUrl;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Linked GitHub ID {GitHubId} to existing user {UserId}", githubProfile.Id, userId);
        return (true, user, null);
    }

    // ============================================================================
    // Private Helper Methods
    // ============================================================================

    private async Task<string?> ExchangeCodeForTokenAsync(string code)
    {
        if (string.IsNullOrEmpty(_config.Auth.GitHubClientId) ||
            string.IsNullOrEmpty(_config.Auth.GitHubClientSecret))
        {
            throw new InvalidOperationException("GitHub OAuth is not configured");
        }

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var requestData = new Dictionary<string, string>
        {
            ["client_id"] = _config.Auth.GitHubClientId,
            ["client_secret"] = _config.Auth.GitHubClientSecret,
            ["code"] = code
        };

        var response = await client.PostAsync(GitHubTokenUrl, new FormUrlEncodedContent(requestData));
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GitHub token exchange failed with status {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var tokenResponse = JsonSerializer.Deserialize<GitHubOAuthTokenResponse>(json);

        return tokenResponse?.AccessToken;
    }

    private async Task<GitHubUserProfile?> FetchGitHubProfileAsync(string accessToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        client.DefaultRequestHeaders.Add("User-Agent", "LRM-Cloud");
        client.DefaultRequestHeaders.Add("Accept", "application/json");

        var response = await client.GetAsync(GitHubUserApiUrl);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("GitHub user API failed with status {StatusCode}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        var profile = JsonSerializer.Deserialize<GitHubUserProfile>(json);
        if (profile == null)
            return null;

        // If /user returned email, it's public and verified by GitHub
        if (!string.IsNullOrEmpty(profile.Email))
        {
            profile.EmailVerified = true;
        }
        else
        {
            // Email is private - fetch from /user/emails endpoint
            var emailResult = await FetchGitHubPrimaryEmailAsync(client);
            if (emailResult.HasValue)
            {
                profile.Email = emailResult.Value.Email;
                profile.EmailVerified = emailResult.Value.Verified;
                _logger.LogDebug("Fetched private email for GitHub {Id}: verified={Verified}",
                    profile.Id, profile.EmailVerified);
            }
            // If fetch failed, Email stays null → placeholder will be used later
        }

        return profile;
    }

    /// <summary>
    /// Fetches the user's primary email from GitHub's /user/emails endpoint.
    /// This is called when /user returns no email (user has email set to private).
    /// </summary>
    /// <returns>Tuple of (email, verified) or null if failed</returns>
    private async Task<(string Email, bool Verified)?> FetchGitHubPrimaryEmailAsync(HttpClient client)
    {
        try
        {
            var response = await client.GetAsync(GitHubEmailsApiUrl);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub emails API failed with status {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var emails = JsonSerializer.Deserialize<List<GitHubEmail>>(json);
            if (emails == null || emails.Count == 0)
            {
                _logger.LogWarning("GitHub emails API returned empty list");
                return null;
            }

            // Priority: primary+verified > any verified > primary > first
            var selected = emails.FirstOrDefault(e => e.Primary && e.Verified)
                        ?? emails.FirstOrDefault(e => e.Verified)
                        ?? emails.FirstOrDefault(e => e.Primary)
                        ?? emails[0];

            return (selected.Email, selected.Verified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching GitHub emails");
            return null;
        }
    }

    /// <summary>
    /// Creates a new user or links GitHub to an existing user.
    /// Returns auth event info for UI notifications.
    /// </summary>
    /// <returns>
    /// (User, IsNewUser, AuthEvent, RelatedEmail) where:
    /// - AuthEvent: "autolinked" if linked to existing, "new_account_email_exists" if email conflict, null otherwise
    /// - RelatedEmail: The email address involved in the event (for UI display)
    /// </returns>
    private async Task<(User User, bool IsNewUser, string? AuthEvent, string? RelatedEmail)> CreateOrLinkUserAsync(
        GitHubUserProfile githubProfile,
        string accessToken)
    {
        // Check if user already exists with this GitHub ID
        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.GitHubId == githubProfile.Id);
        if (existingUser != null)
        {
            // Update existing user's GitHub data
            existingUser.GitHubAccessTokenEncrypted = EncryptToken(accessToken);
            existingUser.GitHubTokenExpiresAt = null; // GitHub tokens don't expire by default
            existingUser.AvatarUrl = githubProfile.AvatarUrl ?? existingUser.AvatarUrl;
            existingUser.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return (existingUser, false, null, null);
        }

        // Check if user exists with same email (potential account linking)
        if (!string.IsNullOrEmpty(githubProfile.Email))
        {
            var githubEmail = githubProfile.Email.ToLower();
            var userByEmail = await _db.Users.FirstOrDefaultAsync(
                u => u.Email!.ToLower() == githubEmail);

            if (userByEmail != null)
            {
                // Security: Only auto-link if BOTH emails are verified
                // This prevents account hijacking via unverified email claims
                if (githubProfile.EmailVerified && userByEmail.EmailVerified)
                {
                    // Safe to link - both parties verified ownership of email
                    userByEmail.GitHubId = githubProfile.Id;
                    userByEmail.GitHubAccessTokenEncrypted = EncryptToken(accessToken);
                    userByEmail.GitHubTokenExpiresAt = null;
                    userByEmail.AvatarUrl = githubProfile.AvatarUrl ?? userByEmail.AvatarUrl;
                    userByEmail.UpdatedAt = DateTime.UtcNow;

                    await _db.SaveChangesAsync();

                    _logger.LogInformation("Auto-linked GitHub {GitHubId} to user {UserId} (both emails verified)",
                        githubProfile.Id, userByEmail.Id);

                    return (userByEmail, false, "autolinked", userByEmail.Email);
                }
                else
                {
                    // Not safe to auto-link - create separate account
                    _logger.LogInformation(
                        "Skipping auto-link for email {Email}: GitHub verified={GitHubVerified}, existing user verified={UserVerified}",
                        githubEmail, githubProfile.EmailVerified, userByEmail.EmailVerified);

                    // Fall through to create new user, but track the conflict for UI notification
                    var conflictEmail = userByEmail.Email;
                    var newUserWithConflict = await CreateNewGitHubUserAsync(githubProfile, accessToken);
                    return (newUserWithConflict, true, "new_account_email_exists", conflictEmail);
                }
            }
        }

        // Create new user (no email conflict)
        var newUser = await CreateNewGitHubUserAsync(githubProfile, accessToken);
        return (newUser, true, null, null);
    }

    /// <summary>
    /// Creates a new user from GitHub profile.
    /// </summary>
    private async Task<User> CreateNewGitHubUserAsync(GitHubUserProfile githubProfile, string accessToken)
    {
        var email = githubProfile.Email ?? $"github_{githubProfile.Id}@lrm-cloud.com";
        var username = await GenerateUniqueUsernameAsync(githubProfile.Login);

        var newUser = new User
        {
            AuthType = "github",
            Email = email,
            Username = username,
            DisplayName = githubProfile.Name,
            AvatarUrl = githubProfile.AvatarUrl,
            // Only mark verified if GitHub confirmed the email
            EmailVerified = githubProfile.EmailVerified,
            GitHubId = githubProfile.Id,
            GitHubAccessTokenEncrypted = EncryptToken(accessToken),
            GitHubTokenExpiresAt = null,
            Plan = "free",
            TranslationCharsLimit = _config.Limits.FreeTranslationChars,
            TranslationCharsResetAt = DateTime.UtcNow.AddMonths(1),
            OtherCharsLimit = _config.Limits.FreeOtherChars,
            OtherCharsResetAt = DateTime.UtcNow.AddMonths(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created new user {UserId} from GitHub ID {GitHubId}", newUser.Id, githubProfile.Id);
        return newUser;
    }

    private async Task<string> GenerateUniqueUsernameAsync(string preferredUsername)
    {
        var username = preferredUsername;
        var counter = 1;

        while (await _db.Users.AnyAsync(u => u.Username == username))
        {
            username = $"{preferredUsername}{counter}";
            counter++;
        }

        return username;
    }

    private string EncryptToken(string token)
    {
        return TokenEncryption.Encrypt(token, _config.Encryption.TokenKey);
    }

    private async Task<(string RefreshToken, DateTime ExpiresAt)> GenerateRefreshTokenAsync(
        int userId,
        string? ipAddress)
    {
        var refreshToken = TokenGenerator.GenerateSecureToken(32);
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken, 12);
        var expiresAt = DateTime.UtcNow.AddDays(_config.Auth.RefreshTokenExpiryDays);

        var refreshTokenEntity = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync();

        return (refreshToken, expiresAt);
    }

    /// <summary>
    /// Syncs user plan limits with current config values.
    /// This ensures users get updated limits if config changes.
    /// </summary>
    private void SyncUserLimitsWithConfig(User user)
    {
        var expectedTranslationLimit = _config.Limits.GetTranslationCharsLimit(user.Plan);
        var expectedOtherLimit = _config.Limits.GetOtherCharsLimit(user.Plan);

        if (user.TranslationCharsLimit != expectedTranslationLimit ||
            user.OtherCharsLimit != expectedOtherLimit)
        {
            _logger.LogInformation(
                "Syncing limits for user {UserId} ({Plan}): Translation {Old}->{New}, Other {OldOther}->{NewOther}",
                user.Id, user.Plan,
                user.TranslationCharsLimit, expectedTranslationLimit,
                user.OtherCharsLimit, expectedOtherLimit);

            user.TranslationCharsLimit = expectedTranslationLimit;
            user.OtherCharsLimit = expectedOtherLimit;
        }
    }
}
