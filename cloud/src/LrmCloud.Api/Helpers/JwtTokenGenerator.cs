using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LrmCloud.Shared.Entities;
using Microsoft.IdentityModel.Tokens;

namespace LrmCloud.Api.Helpers;

public static class JwtTokenGenerator
{
    public static (string Token, DateTime ExpiresAt) GenerateToken(
        User user,
        string jwtSecret,
        int expiryHours)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(jwtSecret);
        var expiresAt = DateTime.UtcNow.AddHours(expiryHours);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, user.Username ?? user.Email!),
            new("auth_type", user.AuthType),
            new("plan", user.Plan),
            new("email_verified", user.EmailVerified.ToString())
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature
            ),
            Issuer = "lrmcloud",
            Audience = "lrmcloud"
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return (tokenString, expiresAt);
    }
}
