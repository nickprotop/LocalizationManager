namespace LrmCloud.Api;

using LrmCloud.Api.Data;
using LrmCloud.Api.Middleware;
using LrmCloud.Api.Services;
using LrmCloud.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using HealthChecks.NpgSql;
using Minio;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

public class Program
{
    public static int Main(string[] args)
    {
        // =============================================================================
        // Serilog Bootstrap (before anything else)
        // =============================================================================

        // Determine log path (Docker: /var/log/lrmcloud/api, Dev: ./logs)
        var logPath = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production"
            ? "/var/log/lrmcloud/api/api-.log"
            : Path.Combine(Directory.GetCurrentDirectory(), "logs", "api-.log");

        // Ensure log directory exists
        var logDir = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("Starting LRM Cloud API");

            var builder = WebApplication.CreateBuilder(args);

            // Use Serilog for all logging
            builder.Host.UseSerilog();

            // =============================================================================
            // Configuration
            // =============================================================================

            // Determine config path (Docker: /app/config.json, Dev: ./config.json)
            var configPath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
            if (!File.Exists(configPath))
            {
                // Try deploy folder for local development
                var deployConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "deploy", "config.json");
                if (File.Exists(deployConfigPath))
                {
                    configPath = deployConfigPath;
                }
            }

            // Load type-safe configuration
            CloudConfiguration config;
            try
            {
                config = ConfigurationExtensions.LoadCloudConfiguration(configPath);
                Log.Information("Loaded configuration from: {ConfigPath}", configPath);
            }
            catch (FileNotFoundException)
            {
                Log.Fatal("config.json not found at {ConfigPath}. Run setup.sh in cloud/deploy/ to create it.", configPath);
                return 1;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Failed to load configuration from {ConfigPath}", configPath);
                return 1;
            }

            // Register configuration in DI
            builder.Services.AddCloudConfiguration(config);

            // =============================================================================
            // Services
            // =============================================================================

            builder.Services.AddHttpClient(); // Required for GitHub OAuth
            builder.Services.AddScoped<IMailService, SmtpMailService>();
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IGitHubAuthService, GitHubAuthService>();
            builder.Services.AddScoped<IOrganizationService, OrganizationService>();
            builder.Services.AddScoped<IProjectService, ProjectService>();
            builder.Services.AddScoped<IResourceService, ResourceService>();
            builder.Services.AddScoped<IStorageService, MinioStorageService>();

            // =============================================================================
            // Database (PostgreSQL + EF Core)
            // =============================================================================

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(config.Database.ConnectionString));

            // =============================================================================
            // Redis (Caching & Sessions)
            // =============================================================================

            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = config.Redis.ConnectionString;
                options.InstanceName = "LrmCloud:";
            });

            // =============================================================================
            // MinIO (Object Storage)
            // =============================================================================

            builder.Services.AddMinio(configureClient => configureClient
                .WithEndpoint(config.Storage.Endpoint)
                .WithCredentials(config.Storage.AccessKey, config.Storage.SecretKey)
                .WithSSL(config.Storage.UseSSL));

            // =============================================================================
            // Health Checks
            // =============================================================================

            builder.Services.AddHealthChecks()
                .AddNpgSql(config.Database.ConnectionString, name: "postgresql")
                .AddRedis(config.Redis.ConnectionString, name: "redis");

            // =============================================================================
            // API & Security
            // =============================================================================

            // JWT Authentication
            var jwtKey = Encoding.UTF8.GetBytes(config.Auth.JwtSecret);
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false; // Set to true in production with HTTPS
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(jwtKey),
                    ValidateIssuer = true,
                    ValidIssuer = "lrmcloud",
                    ValidateAudience = true,
                    ValidAudience = "lrmcloud",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
                options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        if (context.Exception != null)
                        {
                            Log.Warning("JWT authentication failed: {Message}", context.Exception.Message);
                        }
                        return Task.CompletedTask;
                    }
                };
            });

            builder.Services.AddAuthorization();

            builder.Services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                    options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase));
                });

            // Configure ProblemDetails for consistent error responses (RFC 7807)
            builder.Services.AddProblemDetails(options =>
            {
                options.CustomizeProblemDetails = context =>
                {
                    context.ProblemDetails.Extensions["timestamp"] = DateTime.UtcNow;
                    context.ProblemDetails.Instance = context.HttpContext.Request.Path;
                };
            });

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddOpenApi();

            // CORS (will be configured properly when we add the Blazor frontend)
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin() // Will be restricted in production
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });

            // =============================================================================
            // Build App
            // =============================================================================

            var app = builder.Build();

            // =============================================================================
            // Auto-Migrate Database (if enabled)
            // =============================================================================

            if (config.Database.AutoMigrate)
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                try
                {
                    Log.Information("Running database migrations...");
                    db.Database.Migrate();
                    Log.Information("Database migrations completed");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Database migration failed");
                    throw;
                }
            }

            // =============================================================================
            // Middleware Pipeline
            // =============================================================================

            // Serilog request logging (before other middleware)
            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
            });

            // Global exception handler (custom - logs with Serilog, uses our error codes, hides details in production)
            app.ConfigureExceptionHandler(app.Environment);
            app.UseStatusCodePages();

            // Security headers
            app.Use(async (context, next) =>
            {
                context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
                context.Response.Headers.Append("X-Frame-Options", "DENY");
                context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
                context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
                await next();
            });

            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();

            // Development-only features
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
                app.UseDeveloperExceptionPage();
            }

            // =============================================================================
            // Endpoints
            // =============================================================================

            // Health check endpoint (required for deploy.sh)
            app.MapHealthChecks("/health");

            // Ready check (more detailed than health)
            app.MapGet("/ready", () => Results.Ok(new
            {
                status = "ready",
                timestamp = DateTime.UtcNow,
                version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0"
            })).WithName("Ready").WithTags("Health");

            // API info endpoint
            app.MapGet("/", () => Results.Ok(new
            {
                name = "LRM Cloud API",
                version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0",
                environment = app.Environment.EnvironmentName,
                endpoints = new
                {
                    health = "/health",
                    ready = "/ready",
                    api = "/api"
                }
            })).WithName("Root").WithTags("Info");

            // Map controllers (when added)
            app.MapControllers();

            // =============================================================================
            // Run
            // =============================================================================

            Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);
            Log.Information("Listening on: {Urls}", config.Server.Urls);

            app.Run();
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
