using System.Text.RegularExpressions;
using LrmCloud.Shared.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LrmCloud.Api.Services;

/// <summary>
/// SMTP-based email service using MailKit.
/// Supports both authenticated SMTP servers and local sendmail.
/// </summary>
public partial class SmtpMailService : IMailService
{
    private readonly MailConfiguration _config;
    private readonly ILogger<SmtpMailService> _logger;

    public SmtpMailService(MailConfiguration config, ILogger<SmtpMailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, string? textBody = null)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_config.FromName, _config.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody ?? StripHtml(htmlBody)
        };
        message.Body = builder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();

            // Determine security options based on configuration
            var securityOptions = _config.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : (_config.Port == 587 ? SecureSocketOptions.StartTls : SecureSocketOptions.None);

            await client.ConnectAsync(_config.Host, _config.Port, securityOptions);

            // Authenticate only if credentials are provided
            if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.Password))
            {
                await client.AuthenticateAsync(_config.Username, _config.Password);
            }

            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {Recipient}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}: {Subject}", to, subject);
            throw;
        }
    }

    public Task SendTemplateEmailAsync(string to, string templateName, object model)
    {
        // TODO: Implement template rendering when needed
        // For now, this is a placeholder that will be implemented
        // when we add email templates for verification, password reset, etc.
        throw new NotImplementedException($"Email template '{templateName}' not yet implemented");
    }

    /// <summary>
    /// Strips HTML tags from content to create plain text fallback.
    /// </summary>
    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Remove script and style blocks
        var result = ScriptStyleRegex().Replace(html, string.Empty);

        // Replace common block elements with newlines
        result = BlockElementRegex().Replace(result, "\n");

        // Replace br tags with newlines
        result = BrTagRegex().Replace(result, "\n");

        // Remove all remaining HTML tags
        result = HtmlTagRegex().Replace(result, string.Empty);

        // Decode HTML entities
        result = System.Net.WebUtility.HtmlDecode(result);

        // Normalize whitespace
        result = WhitespaceRegex().Replace(result, " ");
        result = MultipleNewlineRegex().Replace(result, "\n\n");

        return result.Trim();
    }

    [GeneratedRegex(@"<(script|style)[^>]*>[\s\S]*?</\1>", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptStyleRegex();

    [GeneratedRegex(@"</(div|p|h[1-6]|li|tr)>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockElementRegex();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BrTagRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex(@"[ \t]+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex MultipleNewlineRegex();
}
