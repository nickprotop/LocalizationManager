namespace LrmCloud.Api.Services;

/// <summary>
/// Email service abstraction for sending transactional emails.
/// </summary>
public interface IMailService
{
    /// <summary>
    /// Sends an email with HTML and optional plain text body.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="subject">Email subject line.</param>
    /// <param name="htmlBody">HTML body content.</param>
    /// <param name="textBody">Optional plain text body. If null, HTML will be stripped.</param>
    Task SendEmailAsync(string to, string subject, string htmlBody, string? textBody = null);

    /// <summary>
    /// Sends an email using a named template with model data.
    /// </summary>
    /// <param name="to">Recipient email address.</param>
    /// <param name="templateName">Name of the email template.</param>
    /// <param name="model">Model data for template rendering.</param>
    Task SendTemplateEmailAsync(string to, string templateName, object model);
}
