using LrmCloud.Api.Services;

namespace LrmCloud.Api.Helpers;

/// <summary>
/// Extension methods for IMailService to handle email sending failures gracefully.
/// </summary>
public static class MailServiceExtensions
{
    /// <summary>
    /// Attempts to send an email, catching and logging any exceptions.
    /// Email failures should not crash the main operation (registration, password reset, etc.)
    /// as the primary action has already succeeded.
    /// </summary>
    /// <returns>True if email sent successfully, false if it failed</returns>
    public static async Task<bool> TrySendEmailAsync(
        this IMailService mailService,
        ILogger logger,
        string to,
        string subject,
        string htmlBody,
        string? textBody = null)
    {
        try
        {
            await mailService.SendEmailAsync(to, subject, htmlBody, textBody);
            return true;
        }
        catch (Exception ex)
        {
            // Log the error but don't throw - email failure shouldn't fail the operation
            logger.LogError(ex,
                "Failed to send email to {Email} with subject '{Subject}'. " +
                "The primary operation succeeded but user notification failed.",
                to, subject);
            return false;
        }
    }

    /// <summary>
    /// Attempts to send a template email, catching and logging any exceptions.
    /// </summary>
    /// <returns>True if email sent successfully, false if it failed</returns>
    public static async Task<bool> TrySendTemplateEmailAsync(
        this IMailService mailService,
        ILogger logger,
        string to,
        string templateName,
        object model)
    {
        try
        {
            await mailService.SendTemplateEmailAsync(to, templateName, model);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send template email '{Template}' to {Email}. " +
                "The primary operation succeeded but user notification failed.",
                templateName, to);
            return false;
        }
    }
}
