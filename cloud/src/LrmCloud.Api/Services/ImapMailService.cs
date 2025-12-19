using System.Reflection;
using System.Text.RegularExpressions;
using LrmCloud.Shared.Configuration;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace LrmCloud.Api.Services;

/// <summary>
/// IMAP-based email service using MailKit.
/// Connects to an existing IMAP mail infrastructure, sends via SMTP submission,
/// and saves sent emails to the Sent folder.
/// </summary>
public partial class ImapMailService : IMailService
{
    private readonly MailConfiguration _config;
    private readonly ILogger<ImapMailService> _logger;
    private readonly Dictionary<string, string> _templateCache = new();
    private readonly object _cacheLock = new();

    // Template configuration: templateName -> (subjectKey, subject)
    private static readonly Dictionary<string, (string SubjectKey, string DefaultSubject)> TemplateConfig = new()
    {
        ["EmailVerification"] = ("verification_subject", "Verify Your Email - LRM Cloud"),
        ["PasswordReset"] = ("reset_subject", "Reset Your Password - LRM Cloud"),
        ["OrganizationInvite"] = ("invite_subject", "You've Been Invited to an Organization - LRM Cloud"),
        ["WelcomeEmail"] = ("welcome_subject", "Welcome to LRM Cloud!")
    };

    public ImapMailService(MailConfiguration config, ILogger<ImapMailService> logger)
    {
        _config = config;
        _logger = logger;

        if (_config.Imap == null)
        {
            throw new InvalidOperationException("IMAP configuration is required when using the IMAP mail backend");
        }
    }

    public async Task SendEmailAsync(string to, string subject, string htmlBody, string? textBody = null)
    {
        var imapConfig = _config.Imap!;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_config.FromName, _config.FromAddress));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Date = DateTimeOffset.UtcNow;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody ?? StripHtml(htmlBody)
        };
        message.Body = builder.ToMessageBody();

        try
        {
            // Send via SMTP submission
            await SendViaSmtpAsync(message, imapConfig);

            // Save to Sent folder via IMAP if configured
            if (imapConfig.SaveToSent)
            {
                await SaveToSentFolderAsync(message, imapConfig);
            }

            _logger.LogInformation("Email sent via IMAP infrastructure to {Recipient}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email via IMAP infrastructure to {Recipient}: {Subject}", to, subject);
            throw;
        }
    }

    /// <summary>
    /// Send the message via SMTP submission server.
    /// </summary>
    private async Task SendViaSmtpAsync(MimeMessage message, ImapConfiguration imapConfig)
    {
        using var client = new SmtpClient();

        var smtpHost = imapConfig.SmtpHost ?? _config.Host;
        var smtpPort = imapConfig.SmtpPort;

        // Determine security options
        var securityOptions = imapConfig.SmtpUseSsl
            ? SecureSocketOptions.SslOnConnect
            : (smtpPort == 587 ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto);

        _logger.LogDebug("Connecting to SMTP server {Host}:{Port}", smtpHost, smtpPort);

        await client.ConnectAsync(smtpHost, smtpPort, securityOptions);

        // Authenticate if credentials are provided
        if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.Password))
        {
            await client.AuthenticateAsync(_config.Username, _config.Password);
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(true);

        _logger.LogDebug("Email sent via SMTP to {Recipient}", message.To);
    }

    /// <summary>
    /// Save the sent message to the Sent folder via IMAP.
    /// </summary>
    private async Task SaveToSentFolderAsync(MimeMessage message, ImapConfiguration imapConfig)
    {
        try
        {
            using var client = new ImapClient();

            var securityOptions = imapConfig.UseSsl
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.Auto;

            _logger.LogDebug("Connecting to IMAP server {Host}:{Port}", imapConfig.Host, imapConfig.Port);

            await client.ConnectAsync(imapConfig.Host, imapConfig.Port, securityOptions);

            if (!string.IsNullOrEmpty(_config.Username) && !string.IsNullOrEmpty(_config.Password))
            {
                await client.AuthenticateAsync(_config.Username, _config.Password);
            }

            // Find or create the Sent folder
            var sentFolder = await GetOrCreateFolderAsync(client, imapConfig.SentFolder);

            if (sentFolder != null)
            {
                await sentFolder.OpenAsync(FolderAccess.ReadWrite);
                await sentFolder.AppendAsync(message, MessageFlags.Seen);
                await sentFolder.CloseAsync();

                _logger.LogDebug("Saved email to {Folder} folder", imapConfig.SentFolder);
            }
            else
            {
                _logger.LogWarning("Could not find or create Sent folder '{Folder}'", imapConfig.SentFolder);
            }

            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            // Don't fail the entire operation if we can't save to Sent
            _logger.LogWarning(ex, "Failed to save email to Sent folder (email was still sent)");
        }
    }

    /// <summary>
    /// Get an existing folder or try to create it.
    /// </summary>
    private async Task<IMailFolder?> GetOrCreateFolderAsync(ImapClient client, string folderName)
    {
        try
        {
            // Try common folder names
            var personalNamespace = client.PersonalNamespaces.FirstOrDefault();
            if (personalNamespace == null)
            {
                return null;
            }

            var rootFolder = await client.GetFolderAsync(personalNamespace.Path);

            // Try to find the folder
            foreach (var folder in await rootFolder.GetSubfoldersAsync())
            {
                if (folder.Name.Equals(folderName, StringComparison.OrdinalIgnoreCase))
                {
                    return folder;
                }

                // Check for common IMAP special-use attributes
                if (folder.Attributes.HasFlag(FolderAttributes.Sent))
                {
                    return folder;
                }
            }

            // Try special-use folder lookup
            try
            {
                var sentFolder = client.GetFolder(SpecialFolder.Sent);
                if (sentFolder != null)
                {
                    return sentFolder;
                }
            }
            catch
            {
                // Special folder not available
            }

            // Try to create the folder
            try
            {
                return await rootFolder.CreateAsync(folderName, true);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not create folder {Folder}", folderName);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error accessing IMAP folders");
            return null;
        }
    }

    public async Task SendTemplateEmailAsync(string to, string templateName, object model)
    {
        // Load and cache the template
        var template = await LoadTemplateAsync(templateName);

        // Render template with model data
        var htmlBody = RenderTemplate(template, model);

        // Get subject from model or use default
        var subject = GetSubjectForTemplate(templateName, model);

        // Send the email
        await SendEmailAsync(to, subject, htmlBody);

        _logger.LogInformation("Sent template email '{Template}' to {Recipient}", templateName, to);
    }

    /// <summary>
    /// Load email template from embedded resources or file system.
    /// Templates are cached after first load.
    /// </summary>
    private async Task<string> LoadTemplateAsync(string templateName)
    {
        lock (_cacheLock)
        {
            if (_templateCache.TryGetValue(templateName, out var cached))
                return cached;
        }

        // Try to load from file system first (for development/customization)
        var basePath = AppContext.BaseDirectory;
        var filePath = Path.Combine(basePath, "Templates", "Email", $"{templateName}.html");

        string template;
        if (File.Exists(filePath))
        {
            template = await File.ReadAllTextAsync(filePath);
            _logger.LogDebug("Loaded email template from file: {Path}", filePath);
        }
        else
        {
            // Fall back to embedded resource
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"LrmCloud.Api.Templates.Email.{templateName}.html";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new InvalidOperationException($"Email template '{templateName}' not found. Expected file at '{filePath}' or embedded resource '{resourceName}'");
            }

            using var reader = new StreamReader(stream);
            template = await reader.ReadToEndAsync();
            _logger.LogDebug("Loaded email template from embedded resource: {Resource}", resourceName);
        }

        lock (_cacheLock)
        {
            _templateCache[templateName] = template;
        }

        return template;
    }

    /// <summary>
    /// Render template by replacing {{ variable }} placeholders with model values.
    /// </summary>
    private string RenderTemplate(string template, object model)
    {
        var values = ModelToDictionary(model);

        return TemplateVariableRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();
            if (values.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? string.Empty;
            }

            _logger.LogWarning("Template variable '{Variable}' not found in model", key);
            return string.Empty;
        });
    }

    /// <summary>
    /// Get subject line for template, checking model for override.
    /// </summary>
    private string GetSubjectForTemplate(string templateName, object model)
    {
        var values = ModelToDictionary(model);

        if (values.TryGetValue("subject", out var subject) && !string.IsNullOrEmpty(subject?.ToString()))
        {
            return subject.ToString()!;
        }

        if (TemplateConfig.TryGetValue(templateName, out var config))
        {
            if (values.TryGetValue(config.SubjectKey, out var configSubject) && !string.IsNullOrEmpty(configSubject?.ToString()))
            {
                return configSubject.ToString()!;
            }
            return config.DefaultSubject;
        }

        return $"LRM Cloud - {templateName}";
    }

    /// <summary>
    /// Convert model object to a flat dictionary of key-value pairs.
    /// </summary>
    private static Dictionary<string, object?> ModelToDictionary(object model)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (model is IDictionary<string, object> modelDict)
        {
            foreach (var kvp in modelDict)
            {
                dict[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            var properties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                dict[prop.Name] = prop.GetValue(model);

                var snakeCase = ToSnakeCase(prop.Name);
                if (snakeCase != prop.Name.ToLower())
                {
                    dict[snakeCase] = prop.GetValue(model);
                }
            }
        }

        return dict;
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return SnakeCaseRegex().Replace(input, "$1_$2").ToLower();
    }

    [GeneratedRegex(@"\{\{\s*([^}]+)\s*\}\}")]
    private static partial Regex TemplateVariableRegex();

    [GeneratedRegex(@"([a-z])([A-Z])")]
    private static partial Regex SnakeCaseRegex();

    /// <summary>
    /// Strips HTML tags from content to create plain text fallback.
    /// </summary>
    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        var result = ScriptStyleRegex().Replace(html, string.Empty);
        result = BlockElementRegex().Replace(result, "\n");
        result = BrTagRegex().Replace(result, "\n");
        result = HtmlTagRegex().Replace(result, string.Empty);
        result = System.Net.WebUtility.HtmlDecode(result);
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
