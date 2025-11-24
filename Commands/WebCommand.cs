// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace LocalizationManager.Commands;

/// <summary>
/// Command to start the web server hosting both the API and Blazor WASM UI.
/// </summary>
public class WebCommand : Command<WebCommand.Settings>
{
    public class Settings : BaseCommandSettings
    {
        [CommandOption("--source-path <PATH>")]
        [Description("Path to source code directory for code scanning. Defaults to parent directory of resource path.")]
        public string? SourcePath { get; set; }

        [CommandOption("--port <PORT>")]
        [Description("Port to bind the web server to. Default: 5000")]
        public int? Port { get; set; }

        [CommandOption("--bind-address <ADDRESS>")]
        [Description("Address to bind the web server to (localhost, 0.0.0.0, *). Default: localhost")]
        public string? BindAddress { get; set; }

        [CommandOption("--no-open-browser")]
        [Description("Do not automatically open browser on startup")]
        public bool NoOpenBrowser { get; set; }

        [CommandOption("--enable-https")]
        [Description("Enable HTTPS")]
        public bool EnableHttps { get; set; }

        [CommandOption("--cert-path <PATH>")]
        [Description("Path to HTTPS certificate file (.pfx)")]
        public string? CertificatePath { get; set; }

        [CommandOption("--cert-password <PASSWORD>")]
        [Description("Password for HTTPS certificate")]
        public string? CertificatePassword { get; set; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        // Load configuration if available
        settings.LoadConfiguration();

        var resourcePath = settings.GetResourcePath();

        // Determine source path for code scanning (same logic as EditCommand)
        // Convert to absolute path first to handle relative paths correctly
        var absoluteResourcePath = Path.GetFullPath(resourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string sourcePath;
        if (settings.SourcePath != null)
        {
            sourcePath = Path.GetFullPath(settings.SourcePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        else
        {
            var parent = Directory.GetParent(absoluteResourcePath);
            sourcePath = parent?.FullName ?? absoluteResourcePath;
        }

        // Determine web server configuration with precedence: CLI args → env vars → config file → defaults
        var port = settings.Port
            ?? (int.TryParse(Environment.GetEnvironmentVariable("LRM_WEB_PORT"), out var envPort) ? envPort : (int?)null)
            ?? settings.LoadedConfiguration?.Web?.Port
            ?? 5000;

        var bindAddress = settings.BindAddress
            ?? Environment.GetEnvironmentVariable("LRM_WEB_BIND_ADDRESS")
            ?? settings.LoadedConfiguration?.Web?.BindAddress
            ?? "localhost";

        var autoOpenBrowser = !settings.NoOpenBrowser
            && (bool.TryParse(Environment.GetEnvironmentVariable("LRM_WEB_AUTO_OPEN_BROWSER"), out var envOpen) ? envOpen : true)
            && (settings.LoadedConfiguration?.Web?.AutoOpenBrowser ?? true);

        var enableHttps = settings.EnableHttps
            || (bool.TryParse(Environment.GetEnvironmentVariable("LRM_WEB_HTTPS_ENABLED"), out var envHttps) && envHttps)
            || (settings.LoadedConfiguration?.Web?.EnableHttps ?? false);

        var certPath = settings.CertificatePath
            ?? Environment.GetEnvironmentVariable("LRM_WEB_HTTPS_CERT_PATH")
            ?? settings.LoadedConfiguration?.Web?.HttpsCertificatePath;

        var certPassword = settings.CertificatePassword
            ?? Environment.GetEnvironmentVariable("LRM_WEB_HTTPS_CERT_PASSWORD")
            ?? settings.LoadedConfiguration?.Web?.HttpsCertificatePassword;

        // Display configuration
        var protocol = enableHttps ? "https" : "http";
        var url = $"{protocol}://{bindAddress}:{port}";

        AnsiConsole.MarkupLine("[bold cyan]Localization Manager - Web Server[/]");
        AnsiConsole.MarkupLine($"[grey]Resource Path:[/] {absoluteResourcePath}");
        AnsiConsole.MarkupLine($"[grey]Source Path:[/] {sourcePath}");
        AnsiConsole.MarkupLine($"[grey]URL:[/] {url}");
        AnsiConsole.WriteLine();

        // Start Kestrel server with API
        var builder = Microsoft.AspNetCore.Builder.WebApplication.CreateBuilder();

        // Add services
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSignalR();

        // Register ConfigurationService for dynamic config reload
        builder.Services.AddSingleton(sp => new LocalizationManager.Core.Configuration.ConfigurationService(absoluteResourcePath));

        // Configure CORS if enabled in configuration
        var corsConfig = settings.LoadedConfiguration?.Web?.Cors;
        if (corsConfig?.Enabled == true && corsConfig.AllowedOrigins?.Count > 0)
        {
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(corsConfig.AllowedOrigins.ToArray())
                          .AllowAnyMethod()
                          .AllowAnyHeader();

                    if (corsConfig.AllowCredentials)
                    {
                        policy.AllowCredentials();
                    }
                });
            });
        }

        // Configure resource path
        builder.Configuration["ResourcePath"] = absoluteResourcePath;
        builder.Configuration["SourcePath"] = sourcePath;

        // Configure Kestrel
        builder.WebHost.UseUrls(url);

        var app = builder.Build();

        // Configure middleware
        if (corsConfig?.Enabled == true && corsConfig.AllowedOrigins?.Count > 0)
        {
            app.UseCors();
        }

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "LRM API v1");
            c.RoutePrefix = "swagger";
        });

        app.MapControllers();

        // Serve static files from wwwroot if it exists
        if (Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot")))
        {
            app.UseStaticFiles();
            app.MapFallbackToFile("index.html");
        }

        AnsiConsole.MarkupLine("[green]✓ Server started successfully![/]");
        AnsiConsole.MarkupLine($"[grey]Swagger UI:[/] {url}/swagger");
        AnsiConsole.MarkupLine("[dim]Press Ctrl+C to stop the server[/]");
        AnsiConsole.WriteLine();

        // Open browser if requested
        if (autoOpenBrowser)
        {
            try
            {
                var browserUrl = Directory.Exists(Path.Combine(AppContext.BaseDirectory, "wwwroot"))
                    ? url
                    : $"{url}/swagger";

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = browserUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore browser open errors
            }
        }

        app.Run();

        return 0;
    }
}
