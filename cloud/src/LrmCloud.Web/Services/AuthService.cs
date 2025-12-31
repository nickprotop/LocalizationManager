using System.Net.Http.Json;
using LrmCloud.Shared.Api;
using LrmCloud.Shared.DTOs.Auth;

using UserProfileDto = LrmCloud.Shared.DTOs.Auth.UserProfileDto;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for authentication operations (login, register, logout, etc.)
/// </summary>
public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly TokenStorageService _tokenStorage;
    private readonly LrmAuthStateProvider _authStateProvider;
    private readonly TokenRefreshCoordinator _refreshCoordinator;

    public AuthService(HttpClient httpClient, TokenStorageService tokenStorage, LrmAuthStateProvider authStateProvider, TokenRefreshCoordinator refreshCoordinator)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
        _authStateProvider = authStateProvider;
        _refreshCoordinator = refreshCoordinator;
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/login", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
                if (result?.Data != null)
                {
                    // Reset any previous auth failure state
                    _refreshCoordinator.ResetFailureState();

                    await _tokenStorage.StoreTokensAsync(
                        result.Data.Token,
                        result.Data.RefreshToken,
                        result.Data.ExpiresAt,
                        result.Data.RefreshTokenExpiresAt);

                    _authStateProvider.NotifyUserAuthentication(result.Data.User);
                    return AuthResult.Success(result.Data.User);
                }
            }

            var error = await ReadErrorMessageAsync(response);
            return AuthResult.Failure(error);
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Login failed: {ex.Message}");
        }
    }

    public async Task<AuthResult> RegisterAsync(RegisterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/register", request);

            if (response.IsSuccessStatusCode)
            {
                // Registration returns a message, not user data (for security - prevents account enumeration)
                var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
                return AuthResult.Success(null, result?.Message ?? "Registration successful. Please check your email to verify your account.");
            }

            var error = await ReadErrorMessageAsync(response);
            return AuthResult.Failure(error);
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Registration failed: {ex.Message}");
        }
    }

    public async Task<AuthResult> ForgotPasswordAsync(string email)
    {
        try
        {
            var request = new ForgotPasswordRequest { Email = email };
            var response = await _httpClient.PostAsJsonAsync("auth/forgot-password", request);

            if (response.IsSuccessStatusCode)
            {
                return AuthResult.Success(null, "If an account exists with this email, a password reset link has been sent.");
            }

            // Don't reveal whether email exists - always return success message
            return AuthResult.Success(null, "If an account exists with this email, a password reset link has been sent.");
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Failed to send reset email: {ex.Message}");
        }
    }

    public async Task<AuthResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/reset-password", request);

            if (response.IsSuccessStatusCode)
            {
                return AuthResult.Success(null, "Password reset successful. You can now log in with your new password.");
            }

            var error = await ReadErrorMessageAsync(response);
            return AuthResult.Failure(error);
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Password reset failed: {ex.Message}");
        }
    }

    public async Task<AuthResult> VerifyEmailAsync(string email, string token)
    {
        try
        {
            var response = await _httpClient.PostAsync($"auth/verify-email?email={Uri.EscapeDataString(email)}&token={Uri.EscapeDataString(token)}", null);

            if (response.IsSuccessStatusCode)
            {
                return AuthResult.Success(null, "Email verified successfully. You can now log in.");
            }

            var error = await ReadErrorMessageAsync(response);
            return AuthResult.Failure(error);
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Email verification failed: {ex.Message}");
        }
    }

    public async Task<bool> RefreshTokenAsync()
    {
        // Use coordinator to prevent concurrent refresh attempts
        if (!await _refreshCoordinator.TryAcquireRefreshLockAsync())
        {
            // Another refresh is in progress or was recently attempted
            // Wait for it to complete and check if tokens are now valid
            await _refreshCoordinator.WaitForRefreshAsync(TokenRefreshCoordinator.MaxRefreshWaitTime);

            // Check if we now have valid tokens (from the other refresh)
            var token = await _tokenStorage.GetAccessTokenAsync();
            return !string.IsNullOrEmpty(token) && !await _tokenStorage.IsTokenExpiredAsync();
        }

        try
        {
            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
            if (string.IsNullOrEmpty(refreshToken))
                return false;

            var request = new RefreshTokenRequest { RefreshToken = refreshToken };
            var response = await _httpClient.PostAsJsonAsync("auth/refresh", request);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
                if (result?.Data != null)
                {
                    await _tokenStorage.StoreTokensAsync(
                        result.Data.Token,
                        result.Data.RefreshToken,
                        result.Data.ExpiresAt,
                        result.Data.RefreshTokenExpiresAt);

                    _authStateProvider.NotifyUserAuthentication(result.Data.User);
                    return true;
                }
            }

            // Only clear tokens if server explicitly rejects the refresh token (401/403)
            // Don't clear on network errors or 5xx errors - those are transient
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                // Mark as permanently failed to prevent retry loops
                _refreshCoordinator.MarkRefreshPermanentlyFailed(
                    "Your session has expired. Please log in again.");

                await _tokenStorage.ClearTokensAsync();
                _authStateProvider.NotifyUserLogout();
            }

            return false;
        }
        catch
        {
            // Network error - don't clear tokens, just return false
            // The user may still be able to retry later
            return false;
        }
        finally
        {
            _refreshCoordinator.ReleaseRefreshLock();
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            var refreshToken = await _tokenStorage.GetRefreshTokenAsync();
            if (!string.IsNullOrEmpty(refreshToken))
            {
                // Try to revoke the refresh token on the server
                await _httpClient.PostAsJsonAsync("auth/logout", new { RefreshToken = refreshToken });
            }
        }
        catch
        {
            // Ignore errors during logout
        }
        finally
        {
            await _tokenStorage.ClearTokensAsync();
            _authStateProvider.NotifyUserLogout();
        }
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("auth/me");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
                return result?.Data;
            }
        }
        catch
        {
            // Ignore errors
        }
        return null;
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await _tokenStorage.GetAccessTokenAsync();
        return !string.IsNullOrEmpty(token);
    }

    /// <summary>
    /// Checks if a refresh attempt should be made.
    /// Returns false if refresh has permanently failed or is already in progress.
    /// </summary>
    public bool ShouldAttemptRefresh()
    {
        // Don't attempt if permanently failed (e.g., token was revoked)
        if (_refreshCoordinator.IsRefreshPermanentlyFailed)
            return false;

        // Don't attempt if already in progress
        if (_refreshCoordinator.IsRefreshInProgress)
            return false;

        return true;
    }

    /// <summary>
    /// Checks if the user has a valid (non-expired) token.
    /// Unlike IsAuthenticatedAsync, this also verifies the token hasn't expired
    /// and handles the case where tokens are being cleared or auth has permanently failed.
    /// </summary>
    public async Task<bool> IsTokenValidAsync()
    {
        // Don't report as authenticated if tokens are being cleared
        // This prevents race conditions during logout
        if (_tokenStorage.IsClearing)
            return false;

        // Don't report as authenticated if refresh has permanently failed
        // This means the session is invalid and user needs to login again
        if (_refreshCoordinator.IsRefreshPermanentlyFailed)
            return false;

        var token = await _tokenStorage.GetAccessTokenAsync();
        if (string.IsNullOrEmpty(token))
            return false;

        // Also check if token is expired
        return !await _tokenStorage.IsTokenExpiredAsync();
    }

    public async Task<ProfileResult> GetProfileAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("auth/me");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserProfileDto>>();
                if (result?.Data != null)
                {
                    return ProfileResult.Success(result.Data);
                }
            }
            var error = await ReadErrorMessageAsync(response);
            return ProfileResult.Failure(error);
        }
        catch (Exception ex)
        {
            return ProfileResult.Failure($"Failed to get profile: {ex.Message}");
        }
    }

    public async Task<ProfileResult> UpdateProfileAsync(UpdateProfileRequest request)
    {
        try
        {
            var response = await _httpClient.PatchAsJsonAsync("auth/profile", request);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserProfileDto>>();
                if (result?.Data != null)
                {
                    return ProfileResult.Success(result.Data, "Profile updated successfully");
                }
            }
            var error = await ReadErrorMessageAsync(response);
            return ProfileResult.Failure(error);
        }
        catch (Exception ex)
        {
            return ProfileResult.Failure($"Failed to update profile: {ex.Message}");
        }
    }

    public async Task<AuthResult> ChangePasswordAsync(ChangePasswordRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/change-password", request);
            if (response.IsSuccessStatusCode)
            {
                return AuthResult.Success(null, "Password changed successfully");
            }
            var error = await ReadErrorMessageAsync(response);
            return AuthResult.Failure(error);
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Failed to change password: {ex.Message}");
        }
    }

    public async Task<AuthResult> UnlinkGitHubAsync(string? password = null)
    {
        try
        {
            var request = new UnlinkGitHubRequest { Password = password };
            var response = await _httpClient.PostAsJsonAsync("auth/github/unlink", request);
            if (response.IsSuccessStatusCode)
            {
                return AuthResult.Success(null, "GitHub account disconnected");
            }
            var error = await ReadErrorMessageAsync(response);
            return AuthResult.Failure(error);
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Failed to unlink GitHub: {ex.Message}");
        }
    }

    /// <summary>
    /// Process tokens received from GitHub OAuth callback (via URL fragment)
    /// </summary>
    public async Task<AuthResult> ProcessGitHubCallbackAsync(string token, DateTime expiresAt, string refreshToken, DateTime refreshTokenExpiresAt)
    {
        try
        {
            await _tokenStorage.StoreTokensAsync(token, refreshToken, expiresAt, refreshTokenExpiresAt);

            // Fetch user info to update auth state
            var user = await GetCurrentUserAsync();
            if (user != null)
            {
                _authStateProvider.NotifyUserAuthentication(user);
                return AuthResult.Success(user);
            }

            return AuthResult.Failure("Failed to get user info after GitHub login");
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Failed to process GitHub login: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Code, string? Error)> InitiateGitHubLinkAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("auth/github/link/initiate", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<GitHubLinkInitiateResponse>>();
                if (result?.Data?.Code != null)
                {
                    return (true, result.Data.Code, null);
                }
                return (false, null, "Failed to get link code");
            }
            var error = await ReadErrorMessageAsync(response);
            return (false, null, error);
        }
        catch (Exception ex)
        {
            return (false, null, $"Failed to initiate GitHub link: {ex.Message}");
        }
    }

    public async Task<AuthResult> DeleteAccountAsync(DeleteAccountRequest request)
    {
        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "auth/account")
            {
                Content = JsonContent.Create(request)
            };
            var response = await _httpClient.SendAsync(httpRequest);

            if (response.IsSuccessStatusCode)
            {
                await _tokenStorage.ClearTokensAsync();
                _authStateProvider.NotifyUserLogout();
                return AuthResult.Success(null, "Account deleted successfully");
            }
            var error = await ReadErrorMessageAsync(response);
            return AuthResult.Failure(error);
        }
        catch (Exception ex)
        {
            return AuthResult.Failure($"Failed to delete account: {ex.Message}");
        }
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync();
            // Try to parse as ProblemDetails
            if (content.Contains("\"detail\""))
            {
                var problem = System.Text.Json.JsonDocument.Parse(content);
                if (problem.RootElement.TryGetProperty("detail", out var detail))
                {
                    return detail.GetString() ?? "An error occurred";
                }
            }
            return content.Length < 200 ? content : "An error occurred";
        }
        catch
        {
            return $"Request failed with status {response.StatusCode}";
        }
    }
}

/// <summary>
/// Result of an authentication operation
/// </summary>
public class AuthResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public UserDto? User { get; private set; }

    public static AuthResult Success(UserDto? user, string? message = null) => new()
    {
        IsSuccess = true,
        User = user,
        SuccessMessage = message
    };

    public static AuthResult Failure(string error) => new()
    {
        IsSuccess = false,
        ErrorMessage = error
    };
}

/// <summary>
/// Result of a profile operation
/// </summary>
public class ProfileResult
{
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }
    public UserProfileDto? Profile { get; private set; }

    public static ProfileResult Success(UserProfileDto profile, string? message = null) => new()
    {
        IsSuccess = true,
        Profile = profile,
        SuccessMessage = message
    };

    public static ProfileResult Failure(string error) => new()
    {
        IsSuccess = false,
        ErrorMessage = error
    };
}
