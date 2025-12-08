using LrmCloud.Shared.DTOs.Auth;

namespace LrmCloud.Api.Services;

public interface IAuthService
{
    Task<bool> RegisterAsync(RegisterRequest request);
    Task<(bool Success, string? ErrorMessage)> VerifyEmailAsync(string email, string token);
    Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<(bool Success, string? ErrorMessage)> ResetPasswordAsync(ResetPasswordRequest request);
    Task<(bool Success, LoginResponse? Response, string? ErrorMessage)> LoginAsync(LoginRequest request, string? ipAddress = null);
    Task<(bool Success, LoginResponse? Response, string? ErrorMessage)> RefreshTokenAsync(string refreshToken, string? ipAddress = null);
    Task<bool> RevokeRefreshTokenAsync(string refreshToken, string? ipAddress = null);
    Task<UserProfileDto?> GetCurrentUserAsync(int userId);
    Task<(bool Success, UserProfileDto? Profile, string? ErrorMessage)> UpdateProfileAsync(int userId, UpdateProfileRequest request);
    Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(int userId, ChangePasswordRequest request);
    Task<(bool Success, string? ErrorMessage)> ChangeEmailAsync(int userId, ChangeEmailRequest request);
    Task<(bool Success, string? ErrorMessage)> VerifyNewEmailAsync(int userId, string token);
    Task<List<SessionDto>> GetSessionsAsync(int userId, string? currentRefreshToken = null);
    Task<(bool Success, string? ErrorMessage)> RevokeSessionAsync(int userId, int sessionId);
    Task<(bool Success, int RevokedCount, string? ErrorMessage)> RevokeAllOtherSessionsAsync(int userId, string currentRefreshToken);
    Task<(bool Success, string? ErrorMessage)> DeleteAccountAsync(int userId, DeleteAccountRequest request);
}
