using System.Text.RegularExpressions;

namespace LrmCloud.Api.Helpers;

public static class PasswordValidator
{
    public static (bool IsValid, string? ErrorMessage) Validate(string password)
    {
        if (password.Length < 12)
            return (false, "Password must be at least 12 characters");

        if (!Regex.IsMatch(password, @"[A-Z]"))
            return (false, "Password must contain at least one uppercase letter");

        if (!Regex.IsMatch(password, @"[a-z]"))
            return (false, "Password must contain at least one lowercase letter");

        if (!Regex.IsMatch(password, @"[0-9]"))
            return (false, "Password must contain at least one digit");

        if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
            return (false, "Password must contain at least one special character");

        return (true, null);
    }
}
