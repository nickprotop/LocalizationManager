using LrmCloud.Api.Data;
using LrmCloud.Api.Helpers;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Auth;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Scriban;

namespace LrmCloud.Api.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IMailService _mailService;
    private readonly CloudConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        AppDbContext db,
        IMailService mailService,
        CloudConfiguration config,
        ILogger<AuthService> logger)
    {
        _db = db;
        _mailService = mailService;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> RegisterAsync(RegisterRequest request)
    {
        // Check if email exists
        var existingUser = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());

        if (existingUser != null)
        {
            // SECURITY: Don't reveal account exists - send "already exists" email
            _logger.LogInformation("Registration attempt for existing email: {Email}", request.Email);

            await SendAccountExistsEmailAsync(request.Email);
            return true; // Pretend success to prevent enumeration
        }

        // Generate verification token (PLAIN - will be sent in email)
        var verificationToken = TokenGenerator.GenerateSecureToken(32);

        // Hash token for storage (NEVER store plain tokens)
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(verificationToken, 12);

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, 12);

        // Create user
        var user = new User
        {
            AuthType = "email",
            Email = request.Email.ToLowerInvariant(),
            EmailVerified = false,
            PasswordHash = passwordHash,
            Username = request.Username,
            EmailVerificationTokenHash = tokenHash,
            EmailVerificationExpiresAt = DateTime.UtcNow.AddHours(_config.Auth.EmailVerificationExpiryHours),
            Plan = "free",
            TranslationCharsLimit = _config.Limits.FreeTranslationChars,
            TranslationCharsResetAt = DateTime.UtcNow.AddMonths(1),
            OtherCharsLimit = _config.Limits.FreeOtherChars,
            OtherCharsResetAt = DateTime.UtcNow.AddMonths(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User registered successfully: {Email}", request.Email);

        // Send verification email with PLAIN token
        await SendVerificationEmailAsync(user, verificationToken);

        return true;
    }

    private async Task SendVerificationEmailAsync(User user, string plainToken)
    {
        try
        {
            var verificationLink = $"{_config.Server.BaseUrl}/verify-email?token={plainToken}&email={Uri.EscapeDataString(user.Email!)}";

            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "Email", "EmailVerification.html");
            var templateText = await File.ReadAllTextAsync(templatePath);
            var template = Template.Parse(templateText);

            var html = await template.RenderAsync(new
            {
                username = user.Username,
                verification_link = verificationLink
            });

            await _mailService.TrySendEmailAsync(
                _logger,
                to: user.Email!,
                subject: "Verify your LRM Cloud email address",
                htmlBody: html
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send verification email to {Email}. User registration succeeded but email notification failed.",
                user.Email);
        }
    }

    private async Task SendAccountExistsEmailAsync(string email)
    {
        try
        {
            var loginLink = $"{_config.Server.BaseUrl}/login";
            var resetLink = $"{_config.Server.BaseUrl}/forgot-password";

            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "Email", "AccountExists.html");
            var templateText = await File.ReadAllTextAsync(templatePath);
            var template = Template.Parse(templateText);

            var html = await template.RenderAsync(new
            {
                email,
                login_link = loginLink,
                reset_link = resetLink
            });

            await _mailService.TrySendEmailAsync(_logger,
                to: email,
                subject: "LRM Cloud registration attempt",
                htmlBody: html
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send account exists email to {Email}. Registration handling succeeded but email notification failed.",
                email);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> VerifyEmailAsync(string email, string token)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant());

        if (user == null)
        {
            return (false, "Invalid verification link");
        }

        if (user.EmailVerified)
        {
            return (false, "Email already verified");
        }

        if (user.EmailVerificationExpiresAt == null || user.EmailVerificationExpiresAt < DateTime.UtcNow)
        {
            return (false, "Verification link expired");
        }

        if (string.IsNullOrEmpty(user.EmailVerificationTokenHash))
        {
            return (false, "Invalid verification token");
        }

        // Verify token by hashing and comparing
        if (!BCrypt.Net.BCrypt.Verify(token, user.EmailVerificationTokenHash))
        {
            return (false, "Invalid verification token");
        }

        // Mark as verified
        user.EmailVerified = true;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Email verified successfully: {Email}", email);

        return (true, null);
    }

    public async Task<bool> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());

        // SECURITY: Always return true to prevent account enumeration
        if (user == null)
        {
            _logger.LogInformation("Password reset requested for non-existent email: {Email}", request.Email);
            return true;
        }

        // Only allow password reset for email auth users
        if (user.AuthType != "email")
        {
            _logger.LogInformation("Password reset requested for OAuth user: {Email}", request.Email);
            return true;
        }

        // Generate reset token (PLAIN - will be sent in email)
        var resetToken = TokenGenerator.GenerateSecureToken(32);

        // Hash token for storage
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(resetToken, 12);

        // Update user with reset token
        user.PasswordResetTokenHash = tokenHash;
        user.PasswordResetExpires = DateTime.UtcNow.AddHours(_config.Auth.PasswordResetExpiryHours);
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Password reset token generated for: {Email}", request.Email);

        // Send password reset email
        await SendPasswordResetEmailAsync(user, resetToken);

        return true;
    }

    private async Task SendPasswordResetEmailAsync(User user, string plainToken)
    {
        try
        {
            var resetLink = $"{_config.Server.BaseUrl}/reset-password?token={plainToken}&email={Uri.EscapeDataString(user.Email!)}";

            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "Email", "PasswordReset.html");
            var templateText = await File.ReadAllTextAsync(templatePath);
            var template = Template.Parse(templateText);

            var html = await template.RenderAsync(new
            {
                username = user.Username,
                reset_link = resetLink
            });

            await _mailService.TrySendEmailAsync(_logger,
                to: user.Email!,
                subject: "Reset your LRM Cloud password",
                htmlBody: html
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send password reset email to {Email}. Password reset token generated but email notification failed.",
                user.Email);
        }
    }

    public async Task<(bool Success, string? ErrorMessage)> ResetPasswordAsync(ResetPasswordRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());

        if (user == null)
            return (false, "Invalid password reset link");

        if (user.AuthType != "email")
            return (false, "Password reset is only available for email accounts");

        if (string.IsNullOrEmpty(user.PasswordResetTokenHash))
        {
            return (false, "No password reset requested");
        }

        if (user.PasswordResetExpires == null || user.PasswordResetExpires < DateTime.UtcNow)
        {
            return (false, "Password reset link expired");
        }

        // Verify token by hashing and comparing
        if (!BCrypt.Net.BCrypt.Verify(request.Token, user.PasswordResetTokenHash))
        {
            return (false, "Invalid password reset token");
        }

        // Hash new password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);

        // Update password and clear reset token
        user.PasswordHash = passwordHash;
        user.PasswordResetTokenHash = null;
        user.PasswordResetExpires = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Password reset successfully for: {Email}", request.Email);

        // Send confirmation email
        await SendPasswordResetSuccessEmailAsync(user);

        return (true, null);
    }

    private async Task SendPasswordResetSuccessEmailAsync(User user)
    {
        try
        {
            var loginLink = $"{_config.Server.BaseUrl}/login";

            var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "Email", "PasswordResetSuccess.html");
            var templateText = await File.ReadAllTextAsync(templatePath);
            var template = Template.Parse(templateText);

            var html = await template.RenderAsync(new
            {
                username = user.Username,
                login_link = loginLink
            });

            await _mailService.TrySendEmailAsync(_logger,
                to: user.Email!,
                subject: "Your LRM Cloud password has been changed",
                htmlBody: html
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send password reset confirmation email to {Email}. Password was changed but email notification failed.",
                user.Email);
        }
    }

    public async Task<(bool Success, LoginResponse? Response, string? ErrorMessage)> LoginAsync(LoginRequest request, string? ipAddress = null)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());

        // Check if account is locked
        if (user != null && user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            var minutesRemaining = (int)Math.Ceiling((user.LockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            return (false, null, $"Account is locked due to too many failed login attempts. Please try again in {minutesRemaining} minute(s).");
        }

        // Validate user exists and uses email auth
        if (user == null || user.AuthType != "email")
        {
            return (false, null, "Invalid email or password");
        }

        // Verify password
        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            // Increment failed attempts
            user.FailedLoginAttempts++;

            // Lock account if threshold reached
            if (user.FailedLoginAttempts >= _config.Auth.MaxFailedLoginAttempts)
            {
                user.LockedUntil = DateTime.UtcNow.AddMinutes(_config.Auth.LockoutDurationMinutes);
                await _db.SaveChangesAsync();

                _logger.LogWarning("Account locked for user: {Email} after {Attempts} failed attempts",
                    user.Email, user.FailedLoginAttempts);

                return (false, null, $"Account locked due to too many failed login attempts. Please try again in {_config.Auth.LockoutDurationMinutes} minutes.");
            }

            await _db.SaveChangesAsync();

            _logger.LogWarning("Failed login attempt for user: {Email} (attempt {Count}/{Max})",
                user.Email, user.FailedLoginAttempts, _config.Auth.MaxFailedLoginAttempts);

            return (false, null, "Invalid email or password");
        }

        // Check if email is verified
        if (!user.EmailVerified)
        {
            return (false, null, "Please verify your email address before logging in");
        }

        // Success - reset failed attempts and update last login
        user.FailedLoginAttempts = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("User logged in successfully: {Email}", user.Email);

        // Generate JWT token
        var (token, expiresAt) = Helpers.JwtTokenGenerator.GenerateToken(
            user,
            _config.Auth.JwtSecret,
            _config.Auth.JwtExpiryHours
        );

        // Generate refresh token
        var (refreshToken, refreshTokenExpiresAt) = await GenerateRefreshTokenAsync(user.Id, ipAddress);

        // Create response
        var response = new LoginResponse
        {
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                Username = user.Username,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                EmailVerified = user.EmailVerified,
                Plan = user.Plan,
                CreatedAt = user.CreatedAt
            },
            Token = token,
            ExpiresAt = expiresAt,
            RefreshToken = refreshToken,
            RefreshTokenExpiresAt = refreshTokenExpiresAt
        };

        return (true, response, null);
    }

    private async Task<(string RefreshToken, DateTime ExpiresAt)> GenerateRefreshTokenAsync(int userId, string? ipAddress)
    {
        // Generate cryptographically secure refresh token
        var refreshToken = TokenGenerator.GenerateSecureToken(32);
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken, 12);
        var expiresAt = DateTime.UtcNow.AddDays(_config.Auth.RefreshTokenExpiryDays);

        // Store in database
        var refreshTokenEntity = new Shared.Entities.RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = ipAddress
        };

        _db.RefreshTokens.Add(refreshTokenEntity);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Refresh token generated for user ID: {UserId}", userId);

        return (refreshToken, expiresAt);
    }

    public async Task<(bool Success, LoginResponse? Response, string? ErrorMessage)> RefreshTokenAsync(string refreshToken, string? ipAddress = null)
    {
        // Find the refresh token in database
        var storedTokens = await _db.RefreshTokens
            .Include(rt => rt.User)
            .Where(rt => rt.User.DeletedAt == null)
            .ToListAsync();

        Shared.Entities.RefreshToken? validToken = null;
        foreach (var storedToken in storedTokens)
        {
            if (BCrypt.Net.BCrypt.Verify(refreshToken, storedToken.TokenHash))
            {
                validToken = storedToken;
                break;
            }
        }

        if (validToken == null)
        {
            _logger.LogWarning("Refresh token not found");
            return (false, null, "Invalid refresh token");
        }

        // Check if token is expired
        if (validToken.IsExpired)
        {
            _logger.LogWarning("Refresh token expired for user ID: {UserId}", validToken.UserId);
            return (false, null, "Refresh token expired");
        }

        // Check if token is revoked
        if (validToken.IsRevoked)
        {
            _logger.LogWarning("Refresh token revoked for user ID: {UserId}", validToken.UserId);
            return (false, null, "Refresh token revoked");
        }

        var user = validToken.User;

        // Check if user is deleted or locked
        if (user.DeletedAt != null)
        {
            return (false, null, "User account not found");
        }

        if (user.LockedUntil.HasValue && user.LockedUntil > DateTime.UtcNow)
        {
            return (false, null, "User account is locked");
        }

        // Track last used timestamp (for session management)
        validToken.LastUsedAt = DateTime.UtcNow;

        // Revoke old refresh token (token rotation)
        validToken.RevokedAt = DateTime.UtcNow;
        validToken.RevokedByIp = ipAddress;

        // Generate new JWT access token
        var (token, expiresAt) = Helpers.JwtTokenGenerator.GenerateToken(
            user,
            _config.Auth.JwtSecret,
            _config.Auth.JwtExpiryHours
        );

        // Generate new refresh token (token rotation)
        var (newRefreshToken, newRefreshTokenExpiresAt) = await GenerateRefreshTokenAsync(user.Id, ipAddress);

        // Store reference to new token in old token (for audit trail)
        validToken.ReplacedByTokenHash = BCrypt.Net.BCrypt.HashPassword(newRefreshToken, 12);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Tokens refreshed for user: {Email}", user.Email);

        // Create response
        var response = new LoginResponse
        {
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                Username = user.Username,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                EmailVerified = user.EmailVerified,
                Plan = user.Plan,
                CreatedAt = user.CreatedAt
            },
            Token = token,
            ExpiresAt = expiresAt,
            RefreshToken = newRefreshToken,
            RefreshTokenExpiresAt = newRefreshTokenExpiresAt
        };

        return (true, response, null);
    }

    public async Task<bool> RevokeRefreshTokenAsync(string refreshToken, string? ipAddress = null)
    {
        // Find the refresh token in database
        var storedTokens = await _db.RefreshTokens
            .Where(rt => rt.RevokedAt == null)
            .ToListAsync();

        Shared.Entities.RefreshToken? validToken = null;
        foreach (var storedToken in storedTokens)
        {
            if (BCrypt.Net.BCrypt.Verify(refreshToken, storedToken.TokenHash))
            {
                validToken = storedToken;
                break;
            }
        }

        if (validToken == null)
        {
            _logger.LogWarning("Refresh token not found for revocation");
            return false;
        }

        // Revoke token
        validToken.RevokedAt = DateTime.UtcNow;
        validToken.RevokedByIp = ipAddress;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Refresh token revoked for user ID: {UserId}", validToken.UserId);

        return true;
    }

    public async Task<UserProfileDto?> GetCurrentUserAsync(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return null;

        return new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email!,
            Username = user.Username,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            EmailVerified = user.EmailVerified,
            AuthType = user.AuthType,
            GitHubId = user.GitHubId,
            Plan = user.Plan,
            StripeCustomerId = user.StripeCustomerId,
            TranslationCharsUsed = user.TranslationCharsUsed,
            TranslationCharsLimit = user.TranslationCharsLimit,
            TranslationCharsResetAt = user.TranslationCharsResetAt,
            OtherCharsUsed = user.OtherCharsUsed,
            OtherCharsLimit = user.OtherCharsLimit,
            OtherCharsResetAt = user.OtherCharsResetAt,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<(bool Success, UserProfileDto? Profile, string? ErrorMessage)> UpdateProfileAsync(int userId, UpdateProfileRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return (false, null, "User not found");

        // Check username uniqueness if username is being changed
        if (!string.IsNullOrEmpty(request.Username) && request.Username != user.Username)
        {
            var usernameExists = await _db.Users
                .AnyAsync(u => u.Username == request.Username && u.Id != userId);

            if (usernameExists)
                return (false, null, "Username is already taken");

            user.Username = request.Username;
        }

        // Update display name if provided
        if (request.DisplayName != null)
        {
            user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName;
        }

        // Update avatar URL if provided
        if (request.AvatarUrl != null)
        {
            user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Profile updated for user: {Email}", user.Email);

        // Return updated profile
        var updatedProfile = new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email!,
            Username = user.Username,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            EmailVerified = user.EmailVerified,
            AuthType = user.AuthType,
            GitHubId = user.GitHubId,
            Plan = user.Plan,
            StripeCustomerId = user.StripeCustomerId,
            TranslationCharsUsed = user.TranslationCharsUsed,
            TranslationCharsLimit = user.TranslationCharsLimit,
            TranslationCharsResetAt = user.TranslationCharsResetAt,
            OtherCharsUsed = user.OtherCharsUsed,
            OtherCharsLimit = user.OtherCharsLimit,
            OtherCharsResetAt = user.OtherCharsResetAt,
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };

        return (true, updatedProfile, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(int userId, ChangePasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return (false, "User not found");
        }

        // Only allow password change for email auth users
        if (user.AuthType != "email")
        {
            return (false, "Password change is only available for email/password accounts");
        }

        // Verify current password
        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return (false, "Current password is incorrect");
        }

        // Validate new password strength
        var (isValid, validationError) = PasswordValidator.Validate(request.NewPassword);
        if (!isValid)
        {
            return (false, validationError);
        }

        // Check if new password is same as current
        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
        {
            return (false, "New password must be different from current password");
        }

        // Hash new password
        var newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 12);

        // Update password
        user.PasswordHash = newPasswordHash;
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke all refresh tokens for this user (force re-login everywhere)
        var userRefreshTokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var token in userRefreshTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = "password-change";
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("Password changed for user: {Email}. All sessions revoked.", user.Email);

        // Send confirmation email (reuse password reset success template)
        await SendPasswordResetSuccessEmailAsync(user);

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangeEmailAsync(int userId, ChangeEmailRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return (false, "User not found");

        // Verify current password
        if (string.IsNullOrEmpty(user.PasswordHash) || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return (false, "Current password is incorrect");

        // Normalize emails for comparison
        var newEmail = request.NewEmail.ToLowerInvariant();
        var currentEmail = user.Email?.ToLowerInvariant();

        // Check if new email is same as current
        if (newEmail == currentEmail)
        {
            return (false, "New email is the same as current email");
        }

        // Check if new email is already in use by another user
        var emailExists = await _db.Users.AnyAsync(u => u.Email!.ToLower() == newEmail && u.Id != userId);
        if (emailExists)
        {
            return (false, "This email address is already in use");
        }

        // Generate verification token
        var verificationToken = TokenGenerator.GenerateSecureToken(32);
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(verificationToken, 12);

        // Set pending email change
        user.PendingEmail = request.NewEmail;
        user.PendingEmailTokenHash = tokenHash;
        user.PendingEmailExpiresAt = DateTime.UtcNow.AddHours(24);
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Email change requested for user {UserId}. New email: {NewEmail}", userId, request.NewEmail);

        // Send verification email to NEW email address
        await SendNewEmailVerificationAsync(user, request.NewEmail, verificationToken);

        // Send notification to OLD email address
        if (!string.IsNullOrEmpty(user.Email))
        {
            await SendEmailChangeNotificationAsync(user, request.NewEmail);
        }

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> VerifyNewEmailAsync(int userId, string token)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return (false, "User not found");
        }

        // Check if there's a pending email change
        if (string.IsNullOrEmpty(user.PendingEmail) || string.IsNullOrEmpty(user.PendingEmailTokenHash))
        {
            return (false, "No pending email change found");
        }

        // Check if token expired
        if (user.PendingEmailExpiresAt == null || user.PendingEmailExpiresAt < DateTime.UtcNow)
        {
            // Clear expired pending change
            user.PendingEmail = null;
            user.PendingEmailTokenHash = null;
            user.PendingEmailExpiresAt = null;
            await _db.SaveChangesAsync();

            return (false, "Email verification link expired. Please request a new email change.");
        }

        // Verify token
        if (!BCrypt.Net.BCrypt.Verify(token, user.PendingEmailTokenHash))
        {
            return (false, "Invalid verification token");
        }

        // Check if new email is still available (might have been taken during verification period)
        var newEmailLower = user.PendingEmail.ToLowerInvariant();
        var emailTaken = await _db.Users.AnyAsync(u => u.Email!.ToLower() == newEmailLower && u.Id != userId);
        if (emailTaken)
        {
            // Clear pending change
            user.PendingEmail = null;
            user.PendingEmailTokenHash = null;
            user.PendingEmailExpiresAt = null;
            await _db.SaveChangesAsync();

            return (false, "This email address is no longer available");
        }

        var oldEmail = user.Email;

        // Update email
        user.Email = user.PendingEmail;
        user.EmailVerified = true; // New email is verified
        user.PendingEmail = null;
        user.PendingEmailTokenHash = null;
        user.PendingEmailExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Email changed successfully for user {UserId}. Old: {OldEmail}, New: {NewEmail}",
            userId, oldEmail, user.Email);

        // Send confirmation to new email
        await SendEmailChangeSuccessAsync(user);

        return (true, null);
    }

    // ============================================================================
    // Private Email Helper Methods
    // ============================================================================

    private async Task SendNewEmailVerificationAsync(User user, string newEmail, string token)
    {
        try
        {
            var verificationLink = $"{_config.Server.BaseUrl}/verify-new-email?token={Uri.EscapeDataString(token)}";

            var subject = "Verify Your New Email Address";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .button {{ display: inline-block; padding: 12px 24px; background-color: #4F46E5; color: white; text-decoration: none; border-radius: 6px; margin: 20px 0; }}
        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <h2>Verify Your New Email Address</h2>
        <p>Hello {user.Username},</p>
        <p>You requested to change your email address for your LRM Cloud account.</p>
        <p>Please click the button below to verify your new email address:</p>
        <a href=""{verificationLink}"" class=""button"">Verify New Email</a>
        <p>Or copy and paste this link into your browser:</p>
        <p style=""word-break: break-all;"">{verificationLink}</p>
        <p>This verification link will expire in 24 hours.</p>
        <p><strong>If you did not request this change, please ignore this email and your email address will remain unchanged.</strong></p>
        <div class=""footer"">
            <p>This is an automated message from LRM Cloud. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            await _mailService.TrySendEmailAsync(_logger, newEmail, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send new email verification to {NewEmail}. Email change request recorded but email notification failed.",
                newEmail);
        }
    }

    private async Task SendEmailChangeNotificationAsync(User user, string newEmail)
    {
        try
        {
            var subject = "Email Change Request";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .alert {{ background-color: #FEF3C7; border-left: 4px solid #F59E0B; padding: 12px; margin: 20px 0; }}
        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <h2>Email Change Request</h2>
        <p>Hello {user.Username},</p>
        <div class=""alert"">
            <p><strong>Important:</strong> A request was made to change your email address from <strong>{user.Email}</strong> to <strong>{newEmail}</strong>.</p>
        </div>
        <p>A verification email has been sent to the new email address. Your current email will remain active until the new email is verified.</p>
        <p><strong>If you did not make this request, please secure your account immediately by changing your password.</strong></p>
        <div class=""footer"">
            <p>This is an automated message from LRM Cloud. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            await _mailService.TrySendEmailAsync(_logger, user.Email!, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email change notification to old email {OldEmail}. Email change request recorded but notification to old address failed.",
                user.Email);
        }
    }

    private async Task SendEmailChangeSuccessAsync(User user)
    {
        try
        {
            var subject = "Email Address Changed Successfully";
            var htmlBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .success {{ background-color: #D1FAE5; border-left: 4px solid: #10B981; padding: 12px; margin: 20px 0; }}
        .footer {{ margin-top: 30px; padding-top: 20px; border-top: 1px solid #ddd; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class=""container"">
        <h2>Email Changed Successfully</h2>
        <p>Hello {user.Username},</p>
        <div class=""success"">
            <p><strong>Success!</strong> Your email address has been changed to <strong>{user.Email}</strong>.</p>
        </div>
        <p>You can now use this email address to log in to your LRM Cloud account.</p>
        <div class=""footer"">
            <p>This is an automated message from LRM Cloud. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            await _mailService.TrySendEmailAsync(_logger, user.Email!, subject, htmlBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email change success confirmation to {NewEmail}. Email was changed but confirmation email failed.",
                user.Email);
        }
    }

    // ============================================================================
    // Session Management Methods
    // ============================================================================

    public async Task<List<SessionDto>> GetSessionsAsync(int userId, string? currentRefreshToken = null)
    {
        // Get all active refresh tokens for this user
        var activeSessions = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null && rt.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(rt => rt.CreatedAt)
            .ToListAsync();

        // Determine which session is current (if refresh token provided)
        string? currentTokenHash = null;
        if (!string.IsNullOrEmpty(currentRefreshToken))
        {
            foreach (var session in activeSessions)
            {
                if (BCrypt.Net.BCrypt.Verify(currentRefreshToken, session.TokenHash))
                {
                    currentTokenHash = session.TokenHash;
                    break;
                }
            }
        }

        // Map to DTOs
        var sessions = activeSessions.Select(rt => new SessionDto
        {
            Id = rt.Id,
            CreatedAt = rt.CreatedAt,
            ExpiresAt = rt.ExpiresAt,
            LastUsedAt = rt.LastUsedAt,
            CreatedByIp = rt.CreatedByIp,
            IsCurrent = rt.TokenHash == currentTokenHash
        }).ToList();

        return sessions;
    }

    public async Task<(bool Success, string? ErrorMessage)> RevokeSessionAsync(int userId, int sessionId)
    {
        var session = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Id == sessionId && rt.UserId == userId);

        if (session == null)
        {
            return (false, "Session not found");
        }

        // Check if already revoked
        if (session.RevokedAt != null)
        {
            return (false, "Session already revoked");
        }

        // Revoke the session
        session.RevokedAt = DateTime.UtcNow;
        session.RevokedByIp = "user-action";

        await _db.SaveChangesAsync();

        _logger.LogInformation("Session {SessionId} revoked by user {UserId}", sessionId, userId);

        return (true, null);
    }

    public async Task<(bool Success, int RevokedCount, string? ErrorMessage)> RevokeAllOtherSessionsAsync(int userId, string currentRefreshToken)
    {
        // Find all active sessions for this user
        var activeSessions = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        // Find current session to exclude
        Shared.Entities.RefreshToken? currentSession = null;
        foreach (var session in activeSessions)
        {
            if (BCrypt.Net.BCrypt.Verify(currentRefreshToken, session.TokenHash))
            {
                currentSession = session;
                break;
            }
        }

        if (currentSession == null)
        {
            return (false, 0, "Current session not found");
        }

        // Revoke all sessions except current
        int revokedCount = 0;
        foreach (var session in activeSessions)
        {
            if (session.Id != currentSession.Id)
            {
                session.RevokedAt = DateTime.UtcNow;
                session.RevokedByIp = "user-action";
                revokedCount++;
            }
        }

        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} revoked {Count} other sessions", userId, revokedCount);

        return (true, revokedCount, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteAccountAsync(int userId, DeleteAccountRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return (false, "User not found");
        }

        // Verify password for confirmation (only if user has password set)
        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return (false, "Incorrect password");
            }
        }
        else
        {
            // GitHub-only users without password should not be able to delete via this endpoint
            return (false, "Password is required. Please set a password first.");
        }

        // Soft delete the account
        user.DeletedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        // Revoke all active refresh tokens (force logout from all sessions)
        var activeSessions = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync();

        foreach (var session in activeSessions)
        {
            session.RevokedAt = DateTime.UtcNow;
            session.RevokedByIp = "account-deletion";
        }

        await _db.SaveChangesAsync();

        // Send confirmation email
        await SendAccountDeletionConfirmationAsync(user);

        _logger.LogInformation("User {UserId} ({Email}) deleted their account", userId, user.Email);

        return (true, null);
    }

    // ============================================================================
    // Email Helpers - Account Deletion
    // ============================================================================

    private async Task SendAccountDeletionConfirmationAsync(User user)
    {
        if (string.IsNullOrEmpty(user.Email))
        {
            return;
        }

        try
        {
            var subject = "Your account has been deleted";
            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; border-radius: 10px 10px 0 0; }}
        .content {{ background: #f9fafb; padding: 30px; border-radius: 0 0 10px 10px; }}
        .info-box {{ background: white; border-left: 4px solid #667eea; padding: 15px; margin: 20px 0; border-radius: 4px; }}
        .footer {{ text-align: center; margin-top: 30px; color: #6b7280; font-size: 14px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1 style=""margin: 0;"">Account Deleted</h1>
        </div>
        <div class=""content"">
            <p>Hello{(string.IsNullOrEmpty(user.Username) ? "" : $" {user.Username}")},</p>

            <p>Your Localization Manager account has been successfully deleted as requested.</p>

            <div class=""info-box"">
                <p style=""margin: 0;""><strong>What happens now:</strong></p>
                <ul style=""margin: 10px 0;"">
                    <li>Your account data has been marked for deletion</li>
                    <li>All active sessions have been logged out</li>
                    <li>You will no longer be able to log in with this email</li>
                    <li>Your data will be permanently removed within 30 days</li>
                </ul>
            </div>

            <p>If you did not request this deletion, please contact our support team immediately at <a href=""mailto:support@lrm-cloud.com"">support@lrm-cloud.com</a>.</p>

            <p>We're sorry to see you go. If you'd like to share feedback about your experience, we'd love to hear from you.</p>

            <p>Best regards,<br>The Localization Manager Team</p>
        </div>
        <div class=""footer"">
            <p>This is an automated message. Please do not reply to this email.</p>
        </div>
    </div>
</body>
</html>";

            await _mailService.TrySendEmailAsync(_logger, user.Email, subject, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send account deletion confirmation to {Email}. Account was deleted but confirmation email failed.",
                user.Email);
        }
    }

    // ============================================================================
    // Plan Management Methods
    // ============================================================================

    public async Task<(bool Success, string? ErrorMessage)> UpdatePlanAsync(int userId, string newPlan)
    {
        var validPlans = new[] { "free", "team", "enterprise" };
        var normalizedPlan = newPlan?.ToLowerInvariant() ?? "free";

        if (!validPlans.Contains(normalizedPlan))
        {
            return (false, $"Invalid plan: {newPlan}. Valid plans are: {string.Join(", ", validPlans)}");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            return (false, "User not found");
        }

        var oldPlan = user.Plan;

        // Update plan
        user.Plan = normalizedPlan;

        // Update limits based on new plan
        user.TranslationCharsLimit = _config.Limits.GetTranslationCharsLimit(normalizedPlan);
        user.OtherCharsLimit = _config.Limits.GetOtherCharsLimit(normalizedPlan);

        // If upgrading, reset counters to give user fresh quota
        if (IsUpgrade(oldPlan, normalizedPlan))
        {
            user.TranslationCharsUsed = 0;
            user.OtherCharsUsed = 0;
            user.TranslationCharsResetAt = DateTime.UtcNow.AddMonths(1);
            user.OtherCharsResetAt = DateTime.UtcNow.AddMonths(1);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} plan changed from {OldPlan} to {NewPlan}",
            userId, oldPlan, normalizedPlan);

        return (true, null);
    }

    private static bool IsUpgrade(string oldPlan, string newPlan)
    {
        var planOrder = new Dictionary<string, int>
        {
            ["free"] = 0,
            ["team"] = 1,
            ["enterprise"] = 2
        };

        var oldOrder = planOrder.GetValueOrDefault(oldPlan?.ToLowerInvariant() ?? "free", 0);
        var newOrder = planOrder.GetValueOrDefault(newPlan?.ToLowerInvariant() ?? "free", 0);

        return newOrder > oldOrder;
    }
}
