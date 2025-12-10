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

    public AuthService(HttpClient httpClient, TokenStorageService tokenStorage, LrmAuthStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _tokenStorage = tokenStorage;
        _authStateProvider = authStateProvider;
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
                var result = await response.Content.ReadFromJsonAsync<ApiResponse<UserDto>>();
                if (result?.Data != null)
                {
                    return AuthResult.Success(result.Data, "Registration successful. Please check your email to verify your account.");
                }
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

    public async Task<AuthResult> VerifyEmailAsync(string token)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("auth/verify-email", new { Token = token });

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

            // Refresh failed, clear tokens
            await LogoutAsync();
            return false;
        }
        catch
        {
            await LogoutAsync();
            return false;
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
