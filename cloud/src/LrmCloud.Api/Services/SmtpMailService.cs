using System.Reflection;
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
    /// Supports nested properties using dot notation: {{ user.name }}
    /// </summary>
    private string RenderTemplate(string template, object model)
    {
        // Convert model to dictionary for easier access
        var values = ModelToDictionary(model);

        // Replace all {{ variable }} placeholders
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

        // Check if subject is provided in model
        if (values.TryGetValue("subject", out var subject) && !string.IsNullOrEmpty(subject?.ToString()))
        {
            return subject.ToString()!;
        }

        // Check template config for subject key
        if (TemplateConfig.TryGetValue(templateName, out var config))
        {
            if (values.TryGetValue(config.SubjectKey, out var configSubject) && !string.IsNullOrEmpty(configSubject?.ToString()))
            {
                return configSubject.ToString()!;
            }
            return config.DefaultSubject;
        }

        // Default fallback
        return $"LRM Cloud - {templateName}";
    }

    /// <summary>
    /// Convert model object to a flat dictionary of key-value pairs.
    /// Supports anonymous types, dictionaries, and POCOs.
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
            // Use reflection to get properties
            var properties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                dict[prop.Name] = prop.GetValue(model);

                // Also add snake_case version for {{ user_name }} style templates
                var snakeCase = ToSnakeCase(prop.Name);
                if (snakeCase != prop.Name.ToLower())
                {
                    dict[snakeCase] = prop.GetValue(model);
                }
            }
        }

        return dict;
    }

    /// <summary>
    /// Convert PascalCase or camelCase to snake_case.
    /// </summary>
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
