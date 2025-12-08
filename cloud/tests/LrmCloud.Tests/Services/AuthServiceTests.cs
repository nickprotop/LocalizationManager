using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Auth;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace LrmCloud.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly Mock<IMailService> _mailServiceMock;
    private readonly Mock<ILogger<AuthService>> _loggerMock;
    private readonly CloudConfiguration _config;
    private readonly AppDbContext _dbContext;
    private readonly AuthService _authService;

    public AuthServiceTests()
    {
        _mailServiceMock = new Mock<IMailService>();
        _loggerMock = new Mock<ILogger<AuthService>>();

        _config = new CloudConfiguration
        {
            Server = new ServerConfiguration
            {
                Urls = "http://localhost:5000",
                Environment = "Test",
                BaseUrl = "https://test.lrm.cloud"
            },
            Database = new DatabaseConfiguration
            {
                ConnectionString = "test",
                AutoMigrate = false
            },
            Redis = new RedisConfiguration
            {
                ConnectionString = "localhost:6379"
            },
            Storage = new StorageConfiguration
            {
                Endpoint = "localhost:9000",
                AccessKey = "test",
                SecretKey = "test",
                Bucket = "test"
            },
            Encryption = new EncryptionConfiguration
            {
                TokenKey = "dGVzdC1rZXktZm9yLWVuY3J5cHRpb24tMzItY2hhcnM="
            },
            Auth = new AuthConfiguration
            {
                JwtSecret = "test-secret-key-for-jwt-tokens-very-long",
                EmailVerificationExpiryHours = 24
            },
            Mail = new MailConfiguration
            {
                Host = "localhost",
                Port = 25,
                FromAddress = "test@test.com",
                FromName = "Test"
            },
            Features = new FeaturesConfiguration(),
            Limits = new LimitsConfiguration()
        };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;
        _dbContext = new AppDbContext(options);

        _authService = new AuthService(_dbContext, _mailServiceMock.Object, _config, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_CreatesUser()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "SecurePass123!",
            Username = "testuser"
        };

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.True(result);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user);
        Assert.Equal("testuser", user.Username);
        Assert.False(user.EmailVerified);
        Assert.Equal("email", user.AuthType);
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_HashesPassword()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "SecurePass123!",
            Username = "testuser"
        };

        // Act
        await _authService.RegisterAsync(request);

        // Assert
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user);
        Assert.NotEqual("SecurePass123!", user.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("SecurePass123!", user.PasswordHash));
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_HashesVerificationToken()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "SecurePass123!",
            Username = "testuser"
        };

        // Act
        await _authService.RegisterAsync(request);

        // Assert
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user);
        Assert.NotNull(user.EmailVerificationTokenHash);
        Assert.StartsWith("$2", user.EmailVerificationTokenHash);  // BCrypt hash prefix
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_SetsExpirationDate()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "SecurePass123!",
            Username = "testuser"
        };

        // Act
        await _authService.RegisterAsync(request);

        // Assert
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user);
        Assert.NotNull(user.EmailVerificationExpiresAt);
        Assert.True(user.EmailVerificationExpiresAt > DateTime.UtcNow);
        Assert.True(user.EmailVerificationExpiresAt <= DateTime.UtcNow.AddHours(24));
    }

    [Fact]
    public async Task RegisterAsync_ValidRequest_SendsVerificationEmail()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "SecurePass123!",
            Username = "testuser"
        };

        // Act
        await _authService.RegisterAsync(request);

        // Assert
        _mailServiceMock.Verify(m => m.SendEmailAsync(
            It.Is<string>(to => to == "test@example.com"),
            It.Is<string>(subject => subject.Contains("Verify")),
            It.Is<string>(html => html.Contains("testuser") && html.Contains("verify")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ExistingEmail_SendsAccountExistsEmail()
    {
        // Arrange
        var existingUser = new User
        {
            AuthType = "email",
            Email = "existing@example.com",
            Username = "existing",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(existingUser);
        await _dbContext.SaveChangesAsync();

        var request = new RegisterRequest
        {
            Email = "existing@example.com",
            Password = "SecurePass123!",
            Username = "newuser"
        };

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        Assert.True(result);  // Returns true to prevent enumeration
        _mailServiceMock.Verify(m => m.SendEmailAsync(
            It.Is<string>(to => to == "existing@example.com"),
            It.IsAny<string>(),
            It.Is<string>(html => html.Contains("already") || html.Contains("exists")),
            It.IsAny<string>()), Times.Once);

        // Verify no new user was created
        var userCount = await _dbContext.Users.CountAsync(u => u.Email == "existing@example.com");
        Assert.Equal(1, userCount);
    }

    [Fact]
    public async Task RegisterAsync_EmailIsCaseInsensitive()
    {
        // Arrange
        var request1 = new RegisterRequest
        {
            Email = "Test@Example.COM",
            Password = "SecurePass123!",
            Username = "testuser"
        };

        var request2 = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "SecurePass123!",
            Username = "testuser2"
        };

        // Act
        await _authService.RegisterAsync(request1);
        await _authService.RegisterAsync(request2);

        // Assert
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == "test@example.com");
        Assert.NotNull(user);
        Assert.Equal("testuser", user.Username); // First one wins

        // Verify only one user was created
        var userCount = await _dbContext.Users.CountAsync();
        Assert.Equal(1, userCount);
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidToken_VerifiesEmail()
    {
        // Arrange
        var token = "test-token";
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(token, 12);

        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerificationTokenHash = tokenHash,
            EmailVerificationExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _authService.VerifyEmailAsync("test@example.com", token);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        var verifiedUser = await _dbContext.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.True(verifiedUser.EmailVerified);
        Assert.Null(verifiedUser.EmailVerificationTokenHash);
        Assert.Null(verifiedUser.EmailVerificationExpiresAt);
    }

    [Fact]
    public async Task VerifyEmailAsync_InvalidToken_ReturnsError()
    {
        // Arrange
        var correctToken = "correct-token";
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(correctToken, 12);

        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerificationTokenHash = tokenHash,
            EmailVerificationExpiresAt = DateTime.UtcNow.AddHours(24),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _authService.VerifyEmailAsync("test@example.com", "wrong-token");

        // Assert
        Assert.False(success);
        Assert.Equal("Invalid verification token", errorMessage);

        var verifiedUser = await _dbContext.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.False(verifiedUser.EmailVerified);
    }

    [Fact]
    public async Task VerifyEmailAsync_ExpiredToken_ReturnsError()
    {
        // Arrange
        var token = "test-token";
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(token, 12);

        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerificationTokenHash = tokenHash,
            EmailVerificationExpiresAt = DateTime.UtcNow.AddHours(-1),  // Expired
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _authService.VerifyEmailAsync("test@example.com", token);

        // Assert
        Assert.False(success);
        Assert.Equal("Verification link expired", errorMessage);
    }

    [Fact]
    public async Task VerifyEmailAsync_AlreadyVerified_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerified = true,  // Already verified
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _authService.VerifyEmailAsync("test@example.com", "any-token");

        // Assert
        Assert.False(success);
        Assert.Equal("Email already verified", errorMessage);
    }

    [Fact]
    public async Task VerifyEmailAsync_NonExistentUser_ReturnsError()
    {
        // Act
        var (success, errorMessage) = await _authService.VerifyEmailAsync("nonexistent@example.com", "any-token");

        // Assert
        Assert.False(success);
        Assert.Equal("Invalid verification link", errorMessage);
    }

    // ========================================
    // Password Reset Tests
    // ========================================

    [Fact]
    public async Task ForgotPasswordAsync_ValidEmail_GeneratesToken()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ForgotPasswordRequest { Email = "test@example.com" };

        // Act
        var result = await _authService.ForgotPasswordAsync(request);

        // Assert
        Assert.True(result);

        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.NotNull(updatedUser.PasswordResetTokenHash);
        Assert.NotNull(updatedUser.PasswordResetExpires);
        Assert.True(updatedUser.PasswordResetExpires > DateTime.UtcNow);
        Assert.StartsWith("$2", updatedUser.PasswordResetTokenHash); // BCrypt hash
    }

    [Fact]
    public async Task ForgotPasswordAsync_ValidEmail_SendsResetEmail()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ForgotPasswordRequest { Email = "test@example.com" };

        // Act
        await _authService.ForgotPasswordAsync(request);

        // Assert
        _mailServiceMock.Verify(m => m.SendEmailAsync(
            It.Is<string>(to => to == "test@example.com"),
            It.Is<string>(subject => subject.Contains("Reset") || subject.Contains("password")),
            It.Is<string>(html => html.Contains("reset") && html.Contains("testuser")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordAsync_NonExistentEmail_ReturnsTrue()
    {
        // Arrange
        var request = new ForgotPasswordRequest { Email = "nonexistent@example.com" };

        // Act
        var result = await _authService.ForgotPasswordAsync(request);

        // Assert
        Assert.True(result); // Prevent enumeration

        // Verify no email was sent
        _mailServiceMock.Verify(m => m.SendEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task ForgotPasswordAsync_OAuthUser_ReturnsTrue()
    {
        // Arrange
        var user = new User
        {
            AuthType = "github",
            Email = "oauth@example.com",
            Username = "oauthuser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ForgotPasswordRequest { Email = "oauth@example.com" };

        // Act
        var result = await _authService.ForgotPasswordAsync(request);

        // Assert
        Assert.True(result); // Prevent enumeration

        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Email == "oauth@example.com");
        Assert.Null(updatedUser.PasswordResetTokenHash); // No token generated for OAuth users
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidToken_ChangesPassword()
    {
        // Arrange
        var resetToken = "test-reset-token";
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(resetToken, 12);

        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword123!", 12),
            EmailVerified = true,
            PasswordResetTokenHash = tokenHash,
            PasswordResetExpires = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = resetToken,
            NewPassword = "NewPassword123!"
        };

        // Act
        var (success, errorMessage) = await _authService.ResetPasswordAsync(request);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword123!", updatedUser.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("OldPassword123!", updatedUser.PasswordHash));
        Assert.Null(updatedUser.PasswordResetTokenHash);
        Assert.Null(updatedUser.PasswordResetExpires);
    }

    [Fact]
    public async Task ResetPasswordAsync_InvalidToken_ReturnsError()
    {
        // Arrange
        var correctToken = "correct-token";
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(correctToken, 12);

        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            PasswordResetTokenHash = tokenHash,
            PasswordResetExpires = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = "wrong-token",
            NewPassword = "NewPassword123!"
        };

        // Act
        var (success, errorMessage) = await _authService.ResetPasswordAsync(request);

        // Assert
        Assert.False(success);
        Assert.Equal("Invalid password reset token", errorMessage);
    }

    [Fact]
    public async Task ResetPasswordAsync_ExpiredToken_ReturnsError()
    {
        // Arrange
        var resetToken = "test-reset-token";
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(resetToken, 12);

        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            PasswordResetTokenHash = tokenHash,
            PasswordResetExpires = DateTime.UtcNow.AddHours(-1), // Expired
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = resetToken,
            NewPassword = "NewPassword123!"
        };

        // Act
        var (success, errorMessage) = await _authService.ResetPasswordAsync(request);

        // Assert
        Assert.False(success);
        Assert.Equal("Password reset link expired", errorMessage);
    }

    [Fact]
    public async Task ResetPasswordAsync_SendsSuccessEmail()
    {
        // Arrange
        var resetToken = "test-reset-token";
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(resetToken, 12);

        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            PasswordResetTokenHash = tokenHash,
            PasswordResetExpires = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ResetPasswordRequest
        {
            Email = "test@example.com",
            Token = resetToken,
            NewPassword = "NewPassword123!"
        };

        // Act
        await _authService.ResetPasswordAsync(request);

        // Assert
        _mailServiceMock.Verify(m => m.SendEmailAsync(
            It.Is<string>(to => to == "test@example.com"),
            It.Is<string>(subject => subject.Contains("password") && subject.Contains("changed")),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    // ============================================================================
    // Login Tests
    // ============================================================================

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var password = "SecurePass123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            Plan = "free",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = password
        };

        // Act
        var (success, response, errorMessage) = await _authService.LoginAsync(request);

        // Assert
        Assert.True(success);
        Assert.NotNull(response);
        Assert.Null(errorMessage);
        Assert.NotNull(response.Token);
        Assert.Equal("test@example.com", response.User.Email);
        Assert.Equal("testuser", response.User.Username);
        Assert.True(response.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_ValidLogin_UpdatesLastLoginAt()
    {
        // Arrange
        var password = "SecurePass123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = password
        };

        // Act
        await _authService.LoginAsync(request);

        // Assert
        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.NotNull(updatedUser.LastLoginAt);
        Assert.True(updatedUser.LastLoginAt >= DateTime.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass123!", 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "WrongPassword123!"
        };

        // Act
        var (success, response, errorMessage) = await _authService.LoginAsync(request);

        // Assert
        Assert.False(success);
        Assert.Null(response);
        Assert.Equal("Invalid email or password", errorMessage);
    }

    [Fact]
    public async Task LoginAsync_NonExistentEmail_ReturnsError()
    {
        // Arrange
        var request = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "SomePassword123!"
        };

        // Act
        var (success, response, errorMessage) = await _authService.LoginAsync(request);

        // Assert
        Assert.False(success);
        Assert.Null(response);
        Assert.Equal("Invalid email or password", errorMessage);
    }

    [Fact]
    public async Task LoginAsync_UnverifiedEmail_ReturnsError()
    {
        // Arrange
        var password = "SecurePass123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = false, // Not verified
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = password
        };

        // Act
        var (success, response, errorMessage) = await _authService.LoginAsync(request);

        // Assert
        Assert.False(success);
        Assert.Null(response);
        Assert.Equal("Please verify your email address before logging in", errorMessage);
    }

    [Fact]
    public async Task LoginAsync_FailedAttempts_IncrementsCounter()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass123!", 12),
            EmailVerified = true,
            FailedLoginAttempts = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "WrongPassword123!"
        };

        // Act
        await _authService.LoginAsync(request);

        // Assert
        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.Equal(1, updatedUser.FailedLoginAttempts);
    }

    [Fact]
    public async Task LoginAsync_MaxFailedAttempts_LocksAccount()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass123!", 12),
            EmailVerified = true,
            FailedLoginAttempts = 4, // One away from max (default is 5)
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = "WrongPassword123!"
        };

        // Act
        var (success, response, errorMessage) = await _authService.LoginAsync(request);

        // Assert
        Assert.False(success);
        Assert.Null(response);
        Assert.Contains("Account locked", errorMessage);

        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.Equal(5, updatedUser.FailedLoginAttempts);
        Assert.NotNull(updatedUser.LockedUntil);
        Assert.True(updatedUser.LockedUntil > DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_LockedAccount_ReturnsError()
    {
        // Arrange
        var password = "SecurePass123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            FailedLoginAttempts = 5,
            LockedUntil = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = password // Even with correct password
        };

        // Act
        var (success, response, errorMessage) = await _authService.LoginAsync(request);

        // Assert
        Assert.False(success);
        Assert.Null(response);
        Assert.Contains("Account is locked", errorMessage);
    }

    [Fact]
    public async Task LoginAsync_SuccessfulLogin_ResetsFailedAttempts()
    {
        // Arrange
        var password = "SecurePass123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            FailedLoginAttempts = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = password
        };

        // Act
        var (success, _, _) = await _authService.LoginAsync(request);

        // Assert
        Assert.True(success);
        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Email == "test@example.com");
        Assert.Equal(0, updatedUser.FailedLoginAttempts);
        Assert.Null(updatedUser.LockedUntil);
    }

    [Fact]
    public async Task LoginAsync_OAuthUser_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "github",
            Email = "oauth@example.com",
            Username = "oauthuser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "oauth@example.com",
            Password = "SomePassword123!"
        };

        // Act
        var (success, response, errorMessage) = await _authService.LoginAsync(request);

        // Assert
        Assert.False(success);
        Assert.Null(response);
        Assert.Equal("Invalid email or password", errorMessage);
    }

    // ============================================================================
    // Refresh Token Tests
    // ============================================================================

    [Fact]
    public async Task LoginAsync_Success_GeneratesRefreshToken()
    {
        // Arrange
        var password = "SecurePass123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            Plan = "free",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new LoginRequest
        {
            Email = "test@example.com",
            Password = password
        };

        // Act
        var (success, response, _) = await _authService.LoginAsync(request, "127.0.0.1");

        // Assert
        Assert.True(success);
        Assert.NotNull(response);
        Assert.NotNull(response.RefreshToken);
        Assert.True(response.RefreshTokenExpiresAt > DateTime.UtcNow);

        // Verify refresh token is stored in database
        var refreshTokens = await _dbContext.RefreshTokens.Where(rt => rt.UserId == user.Id).ToListAsync();
        Assert.Single(refreshTokens);
        Assert.Equal("127.0.0.1", refreshTokens[0].CreatedByIp);
    }

    [Fact]
    public async Task RefreshTokenAsync_ValidToken_ReturnsNewTokens()
    {
        // Arrange
        var password = "SecurePass123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Login to get refresh token
        var (_, loginResponse, _) = await _authService.LoginAsync(
            new LoginRequest { Email = "test@example.com", Password = password });

        // Act
        var (success, refreshResponse, errorMessage) = await _authService.RefreshTokenAsync(
            loginResponse!.RefreshToken, "192.168.1.1");

        // Assert
        Assert.True(success);
        Assert.NotNull(refreshResponse);
        Assert.Null(errorMessage);
        Assert.NotEqual(loginResponse.Token, refreshResponse.Token);
        Assert.NotEqual(loginResponse.RefreshToken, refreshResponse.RefreshToken);
    }

    [Fact]
    public async Task RefreshTokenAsync_TokenRotation_RevokesOldToken()
    {
        // Arrange
        var password = "SecurePass123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Login to get refresh token
        var (_, loginResponse, _) = await _authService.LoginAsync(
            new LoginRequest { Email = "test@example.com", Password = password });
        var oldRefreshToken = loginResponse!.RefreshToken;

        // Act
        await _authService.RefreshTokenAsync(oldRefreshToken);

        // Try to use old token again
        var (success, _, errorMessage) = await _authService.RefreshTokenAsync(oldRefreshToken);

        // Assert - old token should be revoked
        Assert.False(success);
        Assert.Equal("Refresh token revoked", errorMessage);
    }

    [Fact]
    public async Task RefreshTokenAsync_ExpiredToken_ReturnsError()
    {
        // Arrange
        var refreshToken = "test-refresh-token";
        var tokenHash = BCrypt.Net.BCrypt.HashPassword(refreshToken, 12);

        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var expiredToken = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RefreshTokens.Add(expiredToken);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, response, errorMessage) = await _authService.RefreshTokenAsync(refreshToken);

        // Assert
        Assert.False(success);
        Assert.Null(response);
        Assert.Equal("Refresh token expired", errorMessage);
    }

    [Fact]
    public async Task RefreshTokenAsync_InvalidToken_ReturnsError()
    {
        // Arrange
        var invalidToken = "invalid-token-that-does-not-exist";

        // Act
        var (success, response, errorMessage) = await _authService.RefreshTokenAsync(invalidToken);

        // Assert
        Assert.False(success);
        Assert.Null(response);
        Assert.Equal("Invalid refresh token", errorMessage);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_ValidToken_RevokesToken()
    {
        // Arrange
        var password = "SecurePass123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Login to get refresh token
        var (_, loginResponse, _) = await _authService.LoginAsync(
            new LoginRequest { Email = "test@example.com", Password = password });

        // Act
        var revokeSuccess = await _authService.RevokeRefreshTokenAsync(
            loginResponse!.RefreshToken, "10.0.0.1");

        // Assert
        Assert.True(revokeSuccess);

        // Try to use revoked token
        var (success, _, errorMessage) = await _authService.RefreshTokenAsync(loginResponse.RefreshToken);
        Assert.False(success);
        Assert.Equal("Refresh token revoked", errorMessage);

        // Verify IP address was recorded
        var revokedTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.RevokedAt != null)
            .ToListAsync();
        Assert.Single(revokedTokens);
        Assert.Equal("10.0.0.1", revokedTokens[0].RevokedByIp);
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_InvalidToken_ReturnsFalse()
    {
        // Arrange
        var invalidToken = "invalid-token";

        // Act
        var success = await _authService.RevokeRefreshTokenAsync(invalidToken);

        // Assert
        Assert.False(success);
    }

    // ============================================================================
    // Get Current User Tests
    // ============================================================================

    [Fact]
    public async Task GetCurrentUserAsync_ValidUserId_ReturnsProfile()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            DisplayName = "Test User",
            EmailVerified = true,
            Plan = "free",
            TranslationCharsUsed = 5000,
            TranslationCharsLimit = 10000,
            TranslationCharsResetAt = DateTime.UtcNow.AddMonths(1),
            LastLoginAt = DateTime.UtcNow.AddHours(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var profile = await _authService.GetCurrentUserAsync(user.Id);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal(user.Id, profile.Id);
        Assert.Equal(user.Email, profile.Email);
        Assert.Equal(user.Username, profile.Username);
        Assert.Equal(user.DisplayName, profile.DisplayName);
        Assert.Equal(user.EmailVerified, profile.EmailVerified);
        Assert.Equal(user.Plan, profile.Plan);
        Assert.Equal(user.TranslationCharsUsed, profile.TranslationCharsUsed);
        Assert.Equal(user.TranslationCharsLimit, profile.TranslationCharsLimit);
        Assert.Equal(user.LastLoginAt, profile.LastLoginAt);
    }

    [Fact]
    public async Task GetCurrentUserAsync_InvalidUserId_ReturnsNull()
    {
        // Arrange
        var invalidUserId = 99999;

        // Act
        var profile = await _authService.GetCurrentUserAsync(invalidUserId);

        // Assert
        Assert.Null(profile);
    }

    [Fact]
    public async Task GetCurrentUserAsync_GitHubUser_ReturnsGitHubInfo()
    {
        // Arrange
        var user = new User
        {
            AuthType = "github",
            Email = "github@example.com",
            Username = "githubuser",
            EmailVerified = true,
            GitHubId = 12345678,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var profile = await _authService.GetCurrentUserAsync(user.Id);

        // Assert
        Assert.NotNull(profile);
        Assert.Equal("github", profile.AuthType);
        Assert.Equal(12345678, profile.GitHubId);
    }

    // ============================================================================
    // Update Profile Tests
    // ============================================================================

    [Fact]
    public async Task UpdateProfileAsync_UpdateUsername_Success()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "oldusername",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateProfileRequest
        {
            Username = "newusername"
        };

        // Act
        var (success, profile, errorMessage) = await _authService.UpdateProfileAsync(user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(profile);
        Assert.Null(errorMessage);
        Assert.Equal("newusername", profile.Username);

        // Verify in database
        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Equal("newusername", updatedUser.Username);
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdateDisplayNameAndAvatar_Success()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateProfileRequest
        {
            DisplayName = "John Doe",
            AvatarUrl = "https://example.com/avatar.jpg"
        };

        // Act
        var (success, profile, _) = await _authService.UpdateProfileAsync(user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(profile);
        Assert.Equal("John Doe", profile.DisplayName);
        Assert.Equal("https://example.com/avatar.jpg", profile.AvatarUrl);
    }

    [Fact]
    public async Task UpdateProfileAsync_UsernameAlreadyTaken_ReturnsError()
    {
        // Arrange
        var user1 = new User
        {
            AuthType = "email",
            Email = "user1@example.com",
            Username = "existinguser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var user2 = new User
        {
            AuthType = "email",
            Email = "user2@example.com",
            Username = "testuser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.AddRange(user1, user2);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateProfileRequest
        {
            Username = "existinguser" // Try to take user1's username
        };

        // Act
        var (success, profile, errorMessage) = await _authService.UpdateProfileAsync(user2.Id, request);

        // Assert
        Assert.False(success);
        Assert.Null(profile);
        Assert.Equal("Username is already taken", errorMessage);
    }

    [Fact]
    public async Task UpdateProfileAsync_SameUsername_NoError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateProfileRequest
        {
            Username = "testuser", // Same username
            DisplayName = "New Display Name"
        };

        // Act
        var (success, profile, _) = await _authService.UpdateProfileAsync(user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(profile);
        Assert.Equal("testuser", profile.Username);
        Assert.Equal("New Display Name", profile.DisplayName);
    }

    [Fact]
    public async Task UpdateProfileAsync_InvalidUserId_ReturnsError()
    {
        // Arrange
        var request = new UpdateProfileRequest
        {
            Username = "newuser"
        };

        // Act
        var (success, profile, errorMessage) = await _authService.UpdateProfileAsync(99999, request);

        // Assert
        Assert.False(success);
        Assert.Null(profile);
        Assert.Equal("User not found", errorMessage);
    }

    [Fact]
    public async Task UpdateProfileAsync_ClearDisplayName_SetsToNull()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            DisplayName = "Old Display Name",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateProfileRequest
        {
            DisplayName = "" // Empty string should clear it
        };

        // Act
        var (success, profile, _) = await _authService.UpdateProfileAsync(user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(profile);
        Assert.Null(profile.DisplayName);

        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Null(updatedUser.DisplayName);
    }

    [Fact]
    public async Task UpdateProfileAsync_PartialUpdate_OnlyUpdatesProvidedFields()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            DisplayName = "Original Name",
            AvatarUrl = "https://example.com/old.jpg",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new UpdateProfileRequest
        {
            Username = "newusername"
            // DisplayName and AvatarUrl not provided (null)
        };

        // Act
        var (success, profile, _) = await _authService.UpdateProfileAsync(user.Id, request);

        // Assert
        Assert.True(success);
        Assert.NotNull(profile);
        Assert.Equal("newusername", profile.Username);
        Assert.Equal("Original Name", profile.DisplayName); // Should remain unchanged
        Assert.Equal("https://example.com/old.jpg", profile.AvatarUrl); // Should remain unchanged
    }

    // ============================================================================
    // Change Password Tests
    // ============================================================================

    [Fact]
    public async Task ChangePasswordAsync_ValidRequest_ChangesPassword()
    {
        // Arrange
        var currentPassword = "OldPassword123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword, 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ChangePasswordRequest
        {
            CurrentPassword = currentPassword,
            NewPassword = "NewPassword456!"
        };

        // Act
        var (success, errorMessage) = await _authService.ChangePasswordAsync(user.Id, request);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        // Verify password was changed
        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Id == user.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword456!", updatedUser.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify(currentPassword, updatedUser.PasswordHash));
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!", 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "WrongPassword123!",
            NewPassword = "NewPassword456!"
        };

        // Act
        var (success, errorMessage) = await _authService.ChangePasswordAsync(user.Id, request);

        // Assert
        Assert.False(success);
        Assert.Equal("Current password is incorrect", errorMessage);

        // Verify password was not changed
        var updatedUser = await _dbContext.Users.FirstAsync(u => u.Id == user.Id);
        Assert.True(BCrypt.Net.BCrypt.Verify("CorrectPassword123!", updatedUser.PasswordHash));
    }

    [Fact]
    public async Task ChangePasswordAsync_OAuthUser_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "github",
            Email = "github@example.com",
            Username = "githubuser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "SomePassword123!",
            NewPassword = "NewPassword456!"
        };

        // Act
        var (success, errorMessage) = await _authService.ChangePasswordAsync(user.Id, request);

        // Assert
        Assert.False(success);
        Assert.Equal("Password change is only available for email/password accounts", errorMessage);
    }

    [Fact]
    public async Task ChangePasswordAsync_NewPasswordSameAsCurrent_ReturnsError()
    {
        // Arrange
        var password = "SamePassword123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ChangePasswordRequest
        {
            CurrentPassword = password,
            NewPassword = password // Same as current
        };

        // Act
        var (success, errorMessage) = await _authService.ChangePasswordAsync(user.Id, request);

        // Assert
        Assert.False(success);
        Assert.Equal("New password must be different from current password", errorMessage);
    }

    [Fact]
    public async Task ChangePasswordAsync_WeakNewPassword_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("StrongPassword123!", 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ChangePasswordRequest
        {
            CurrentPassword = "StrongPassword123!",
            NewPassword = "weak" // Too weak
        };

        // Act
        var (success, errorMessage) = await _authService.ChangePasswordAsync(user.Id, request);

        // Assert
        Assert.False(success);
        Assert.NotNull(errorMessage);
        // Password validation error message will contain details about requirements
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_RevokesAllRefreshTokens()
    {
        // Arrange
        var currentPassword = "OldPassword123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword, 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Create multiple refresh tokens for this user
        var token1 = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword("token1", 12),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        var token2 = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword("token2", 12),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RefreshTokens.AddRange(token1, token2);
        await _dbContext.SaveChangesAsync();

        var request = new ChangePasswordRequest
        {
            CurrentPassword = currentPassword,
            NewPassword = "NewPassword456!"
        };

        // Act
        var (success, _) = await _authService.ChangePasswordAsync(user.Id, request);

        // Assert
        Assert.True(success);

        // Verify all refresh tokens are revoked
        var refreshTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id)
            .ToListAsync();
        Assert.All(refreshTokens, rt =>
        {
            Assert.NotNull(rt.RevokedAt);
            Assert.Equal("password-change", rt.RevokedByIp);
        });
    }

    [Fact]
    public async Task ChangePasswordAsync_Success_SendsConfirmationEmail()
    {
        // Arrange
        var currentPassword = "OldPassword123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(currentPassword, 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new ChangePasswordRequest
        {
            CurrentPassword = currentPassword,
            NewPassword = "NewPassword456!"
        };

        // Act
        await _authService.ChangePasswordAsync(user.Id, request);

        // Assert
        _mailServiceMock.Verify(m => m.SendEmailAsync(
            It.Is<string>(to => to == "test@example.com"),
            It.Is<string>(subject => subject.Contains("password") && subject.Contains("changed")),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ChangePasswordAsync_InvalidUserId_ReturnsError()
    {
        // Arrange
        var request = new ChangePasswordRequest
        {
            CurrentPassword = "OldPassword123!",
            NewPassword = "NewPassword456!"
        };

        // Act
        var (success, errorMessage) = await _authService.ChangePasswordAsync(99999, request);

        // Assert
        Assert.False(success);
        Assert.Equal("User not found", errorMessage);
    }

    // ============================================================================
    // Session Management Tests
    // ============================================================================

    [Fact]
    public async Task GetSessionsAsync_ReturnsActiveSessions()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!", 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Create multiple active refresh tokens
        var token1 = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword("token1", 12),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddHours(-5),
            CreatedByIp = "192.168.1.1",
            LastUsedAt = DateTime.UtcNow.AddHours(-1)
        };
        var token2 = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword("token2", 12),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            CreatedByIp = "192.168.1.2"
        };
        _dbContext.RefreshTokens.AddRange(token1, token2);
        await _dbContext.SaveChangesAsync();

        // Act
        var sessions = await _authService.GetSessionsAsync(user.Id);

        // Assert
        Assert.Equal(2, sessions.Count);
        Assert.Contains(sessions, s => s.CreatedByIp == "192.168.1.1");
        Assert.Contains(sessions, s => s.CreatedByIp == "192.168.1.2");
        Assert.All(sessions, s => Assert.False(s.IsCurrent)); // No current token provided
    }

    [Fact]
    public async Task GetSessionsAsync_IdentifiesCurrentSession()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var currentToken = "current-token";
        var token1 = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(currentToken, 12),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = "192.168.1.1"
        };
        var token2 = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword("other-token", 12),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            CreatedByIp = "192.168.1.2"
        };
        _dbContext.RefreshTokens.AddRange(token1, token2);
        await _dbContext.SaveChangesAsync();

        // Act
        var sessions = await _authService.GetSessionsAsync(user.Id, currentToken);

        // Assert
        Assert.Equal(2, sessions.Count);
        var currentSession = sessions.First(s => s.CreatedByIp == "192.168.1.1");
        var otherSession = sessions.First(s => s.CreatedByIp == "192.168.1.2");
        Assert.True(currentSession.IsCurrent);
        Assert.False(otherSession.IsCurrent);
    }

    [Fact]
    public async Task GetSessionsAsync_ExcludesRevokedAndExpiredTokens()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var activeToken = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash1",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = "192.168.1.1"
        };
        var revokedToken = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash2",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = "192.168.1.2",
            RevokedAt = DateTime.UtcNow.AddHours(-1)
        };
        var expiredToken = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash3",
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = "192.168.1.3"
        };
        _dbContext.RefreshTokens.AddRange(activeToken, revokedToken, expiredToken);
        await _dbContext.SaveChangesAsync();

        // Act
        var sessions = await _authService.GetSessionsAsync(user.Id);

        // Assert
        Assert.Single(sessions);
        Assert.Equal("192.168.1.1", sessions[0].CreatedByIp);
    }

    [Fact]
    public async Task RevokeSessionAsync_ValidSession_RevokesSession()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = "hash",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = "192.168.1.1"
        };
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _authService.RevokeSessionAsync(user.Id, token.Id);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        var revokedToken = await _dbContext.RefreshTokens.FirstAsync(rt => rt.Id == token.Id);
        Assert.NotNull(revokedToken.RevokedAt);
        Assert.Equal("user-action", revokedToken.RevokedByIp);
    }

    [Fact]
    public async Task RevokeSessionAsync_NonExistentSession_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _authService.RevokeSessionAsync(user.Id, 99999);

        // Assert
        Assert.False(success);
        Assert.Equal("Session not found", errorMessage);
    }

    [Fact]
    public async Task RevokeSessionAsync_AlreadyRevokedSession_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = DateTime.UtcNow.AddHours(-1) // Already revoked
        };
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, errorMessage) = await _authService.RevokeSessionAsync(user.Id, token.Id);

        // Assert
        Assert.False(success);
        Assert.Equal("Session already revoked", errorMessage);
    }

    [Fact]
    public async Task RevokeSessionAsync_OtherUsersSession_ReturnsError()
    {
        // Arrange
        var user1 = new User
        {
            AuthType = "email",
            Email = "user1@example.com",
            Username = "user1",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var user2 = new User
        {
            AuthType = "email",
            Email = "user2@example.com",
            Username = "user2",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.AddRange(user1, user2);
        await _dbContext.SaveChangesAsync();

        var token = new Shared.Entities.RefreshToken
        {
            UserId = user1.Id,
            TokenHash = "hash",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        // Act - user2 tries to revoke user1's session
        var (success, errorMessage) = await _authService.RevokeSessionAsync(user2.Id, token.Id);

        // Assert
        Assert.False(success);
        Assert.Equal("Session not found", errorMessage);
    }

    [Fact]
    public async Task RevokeAllOtherSessionsAsync_RevokesAllExceptCurrent()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var currentToken = "current-token";
        var token1 = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(currentToken, 12),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = "192.168.1.1"
        };
        var token2 = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword("other-token-1", 12),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            CreatedByIp = "192.168.1.2"
        };
        var token3 = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword("other-token-2", 12),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            CreatedByIp = "192.168.1.3"
        };
        _dbContext.RefreshTokens.AddRange(token1, token2, token3);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, revokedCount, errorMessage) = await _authService.RevokeAllOtherSessionsAsync(user.Id, currentToken);

        // Assert
        Assert.True(success);
        Assert.Equal(2, revokedCount);
        Assert.Null(errorMessage);

        // Verify current session is not revoked
        var currentSession = await _dbContext.RefreshTokens.FirstAsync(rt => rt.Id == token1.Id);
        Assert.Null(currentSession.RevokedAt);

        // Verify other sessions are revoked
        var otherSessions = await _dbContext.RefreshTokens
            .Where(rt => rt.Id != token1.Id && rt.UserId == user.Id)
            .ToListAsync();
        Assert.All(otherSessions, rt =>
        {
            Assert.NotNull(rt.RevokedAt);
            Assert.Equal("user-action", rt.RevokedByIp);
        });
    }

    [Fact]
    public async Task RevokeAllOtherSessionsAsync_InvalidCurrentToken_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var token = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword("valid-token", 12),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, revokedCount, errorMessage) = await _authService.RevokeAllOtherSessionsAsync(user.Id, "invalid-token");

        // Assert
        Assert.False(success);
        Assert.Equal(0, revokedCount);
        Assert.Equal("Current session not found", errorMessage);
    }

    [Fact]
    public async Task RevokeAllOtherSessionsAsync_OnlyOneSession_RevokesNone()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var currentToken = "only-token";
        var token = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = BCrypt.Net.BCrypt.HashPassword(currentToken, 12),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RefreshTokens.Add(token);
        await _dbContext.SaveChangesAsync();

        // Act
        var (success, revokedCount, errorMessage) = await _authService.RevokeAllOtherSessionsAsync(user.Id, currentToken);

        // Assert
        Assert.True(success);
        Assert.Equal(0, revokedCount);
        Assert.Null(errorMessage);

        // Verify the only session is still active
        var session = await _dbContext.RefreshTokens.FirstAsync(rt => rt.Id == token.Id);
        Assert.Null(session.RevokedAt);
    }

    // ============================================================================
    // Account Deletion Tests
    // ============================================================================

    [Fact]
    public async Task DeleteAccountAsync_ValidRequest_SoftDeletesAccount()
    {
        // Arrange
        var password = "Password123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new DeleteAccountRequest
        {
            Password = password
        };

        // Act
        var (success, errorMessage) = await _authService.DeleteAccountAsync(user.Id, request);

        // Assert
        Assert.True(success);
        Assert.Null(errorMessage);

        // Verify account is soft deleted
        var deletedUser = await _dbContext.Users.IgnoreQueryFilters().FirstAsync(u => u.Id == user.Id);
        Assert.NotNull(deletedUser.DeletedAt);
        Assert.True(deletedUser.DeletedAt <= DateTime.UtcNow);
    }

    [Fact]
    public async Task DeleteAccountAsync_WrongPassword_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!", 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new DeleteAccountRequest
        {
            Password = "WrongPassword123!"
        };

        // Act
        var (success, errorMessage) = await _authService.DeleteAccountAsync(user.Id, request);

        // Assert
        Assert.False(success);
        Assert.Equal("Incorrect password", errorMessage);

        // Verify account is not deleted
        var notDeletedUser = await _dbContext.Users.FirstAsync(u => u.Id == user.Id);
        Assert.Null(notDeletedUser.DeletedAt);
    }

    [Fact]
    public async Task DeleteAccountAsync_GitHubUserWithoutPassword_ReturnsError()
    {
        // Arrange
        var user = new User
        {
            AuthType = "github",
            Email = "github@example.com",
            Username = "githubuser",
            EmailVerified = true,
            GitHubId = 12345678,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new DeleteAccountRequest
        {
            Password = "SomePassword123!"
        };

        // Act
        var (success, errorMessage) = await _authService.DeleteAccountAsync(user.Id, request);

        // Assert
        Assert.False(success);
        Assert.Equal("Password is required. Please set a password first.", errorMessage);
    }

    [Fact]
    public async Task DeleteAccountAsync_Success_RevokesAllSessions()
    {
        // Arrange
        var password = "Password123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        // Create multiple active sessions
        var token1 = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash1",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        var token2 = new Shared.Entities.RefreshToken
        {
            UserId = user.Id,
            TokenHash = "hash2",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.RefreshTokens.AddRange(token1, token2);
        await _dbContext.SaveChangesAsync();

        var request = new DeleteAccountRequest
        {
            Password = password
        };

        // Act
        await _authService.DeleteAccountAsync(user.Id, request);

        // Assert - all sessions should be revoked
        var sessions = await _dbContext.RefreshTokens.Where(rt => rt.UserId == user.Id).ToListAsync();
        Assert.All(sessions, rt =>
        {
            Assert.NotNull(rt.RevokedAt);
            Assert.Equal("account-deletion", rt.RevokedByIp);
        });
    }

    [Fact]
    public async Task DeleteAccountAsync_Success_SendsConfirmationEmail()
    {
        // Arrange
        var password = "Password123!";
        var user = new User
        {
            AuthType = "email",
            Email = "test@example.com",
            Username = "testuser",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, 12),
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        var request = new DeleteAccountRequest
        {
            Password = password
        };

        // Act
        await _authService.DeleteAccountAsync(user.Id, request);

        // Assert
        _mailServiceMock.Verify(m => m.SendEmailAsync(
            It.Is<string>(to => to == "test@example.com"),
            It.Is<string>(subject => subject.Contains("deleted")),
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAccountAsync_InvalidUserId_ReturnsError()
    {
        // Arrange
        var request = new DeleteAccountRequest
        {
            Password = "Password123!"
        };

        // Act
        var (success, errorMessage) = await _authService.DeleteAccountAsync(99999, request);

        // Assert
        Assert.False(success);
        Assert.Equal("User not found", errorMessage);
    }
}
