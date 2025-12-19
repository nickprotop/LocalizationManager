namespace LrmCloud.Api;

using LrmCloud.Api.Authentication;
using LrmCloud.Api.Authorization;
using LrmCloud.Api.BackgroundJobs;
using LrmCloud.Api.Data;
using LrmCloud.Api.Middleware;
using LrmCloud.Api.Services;
using LrmCloud.Api.Services.Billing;
using LrmCloud.Api.Services.Billing.Providers;
using LrmCloud.Api.Services.Translation;
using LrmCloud.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using HealthChecks.NpgSql;
using Minio;
using Serilog;
using Serilog.Events;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http.Features;

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
            // Security: Request Size Limits
            // =============================================================================

            // Limit request body size to prevent large file upload attacks
            builder.Services.Configure<FormOptions>(options =>
            {
                options.ValueLengthLimit = 10_485_760;            // 10MB per value
                options.MultipartBodyLengthLimit = 52_428_800;    // 50MB total for multipart
                options.MemoryBufferThreshold = 1_048_576;        // 1MB before buffering to disk
            });

            // Also set Kestrel limits
            builder.WebHost.ConfigureKestrel(serverOptions =>
            {
                serverOptions.Limits.MaxRequestBodySize = 52_428_800; // 50MB max request body
            });

            // =============================================================================
            // Services
            // =============================================================================

            builder.Services.AddHttpClient(); // Required for GitHub OAuth

            // Register mail service based on configuration
            if (config.Mail.Backend.Equals("imap", StringComparison.OrdinalIgnoreCase))
            {
                builder.Services.AddScoped<IMailService, ImapMailService>();
            }
            else
            {
                builder.Services.AddScoped<IMailService, SmtpMailService>();
            }
            builder.Services.AddScoped<IAuthService, AuthService>();
            builder.Services.AddScoped<IGitHubAuthService, GitHubAuthService>();
            builder.Services.AddScoped<IOrganizationService, OrganizationService>();
            builder.Services.AddScoped<IProjectService, ProjectService>();
            builder.Services.AddScoped<IResourceService, ResourceService>();
            builder.Services.AddScoped<ResourceSyncService>(); // File-based sync with Core backends
            builder.Services.AddScoped<SnapshotService>(); // Point-in-time snapshot management
            builder.Services.AddScoped<TranslationMemoryService>(); // Translation Memory for reuse
            builder.Services.AddScoped<GlossaryService>(); // Glossary management for consistent terminology
            builder.Services.AddScoped<ReviewWorkflowService>(); // Review/approval workflow for translations
            builder.Services.AddScoped<IStorageService, MinioStorageService>();

            // Translation Services
            builder.Services.AddSingleton<IApiKeyEncryptionService, ApiKeyEncryptionService>();
            builder.Services.AddScoped<IApiKeyHierarchyService, ApiKeyHierarchyService>();
            builder.Services.AddScoped<ILrmTranslationProvider, LrmTranslationProvider>();
            builder.Services.AddScoped<ICloudTranslationService, CloudTranslationService>();

            // CLI API Key Service
            builder.Services.AddScoped<ICliApiKeyService, CliApiKeyService>();

            // Usage Statistics Service
            builder.Services.AddScoped<IUsageService, UsageService>();

            // Payment Provider Factory & Providers
            builder.Services.AddSingleton<PaymentProviderFactory>();
            builder.Services.AddSingleton<StripePaymentProvider>();
            builder.Services.AddSingleton<PayPalPaymentProvider>();
            builder.Services.AddHttpClient("PayPal"); // Required for PayPal REST API

            // Billing Service
            builder.Services.AddScoped<IBillingService, BillingService>();

            // Authorization Service
            builder.Services.AddScoped<ILrmAuthorizationService, LrmAuthorizationService>();

            // Background Jobs
            builder.Services.AddHostedService<UsageResetService>();

            // =============================================================================
            // Database (PostgreSQL + EF Core)
            // =============================================================================

            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseNpgsql(config.Database.ConnectionString,
                    npgsqlOptions => npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery)));

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

            // Authentication: JWT Bearer + API Key with policy-based scheme selection
            var jwtKey = Encoding.UTF8.GetBytes(config.Auth.JwtSecret);
            builder.Services.AddAuthentication(options =>
            {
                // Use policy scheme that selects between API Key and JWT based on request headers
                options.DefaultAuthenticateScheme = "ApiKeyOrJwt";
                options.DefaultChallengeScheme = "ApiKeyOrJwt";
            })
            // API Key authentication scheme
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationOptions.DefaultScheme, _ => { })
            // JWT Bearer authentication scheme
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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
            })
            // Policy scheme to select between API Key and JWT based on presence of X-API-Key header
            .AddPolicyScheme("ApiKeyOrJwt", "API Key or JWT", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    // If X-API-Key header is present, use API key authentication
                    if (context.Request.Headers.ContainsKey(ApiKeyAuthenticationOptions.HeaderName))
                    {
                        return ApiKeyAuthenticationOptions.DefaultScheme;
                    }
                    // Otherwise fall back to JWT Bearer
                    return JwtBearerDefaults.AuthenticationScheme;
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

            // CORS Configuration
            var corsConfig = config.Server.Cors;
            var corsMode = corsConfig?.Mode?.ToLowerInvariant() ?? "allow-all";

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    switch (corsMode)
                    {
                        case "same-origin":
                            // No CORS - only same-origin requests allowed
                            // Don't configure any CORS policy (effectively blocks cross-origin)
                            break;

                        case "whitelist" when corsConfig?.AllowedOrigins?.Count > 0:
                            // Specific origins only
                            policy.WithOrigins(corsConfig.AllowedOrigins.ToArray())
                                .AllowAnyMethod()
                                .AllowAnyHeader();
                            if (corsConfig.AllowCredentials)
                            {
                                policy.AllowCredentials();
                            }
                            break;

                        case "allow-all":
                        default:
                            // Allow any origin (default - for development/testing)
                            policy.AllowAnyOrigin()
                                .AllowAnyMethod()
                                .AllowAnyHeader();
                            break;
                    }
                });
            });

            // =============================================================================
            // Rate Limiting
            // =============================================================================

            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Global rate limit: 100 requests/minute per IP
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 100,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                // Strict auth rate limit: 10 requests/minute per IP
                options.AddPolicy("auth", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                // Translation rate limit: 30 requests/minute per user (expensive API calls)
                options.AddPolicy("translation", context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: context.User?.Identity?.Name ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 30,
                            Window = TimeSpan.FromMinutes(1)
                        }));

                // Rate limit exceeded response
                options.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        error = "Too many requests. Please try again later.",
                        retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                            ? retryAfter.TotalSeconds
                            : 60
                    }, cancellationToken);
                };
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

            // Rate limiting (before routing to block excessive requests early)
            app.UseRateLimiter();

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
            // Register Payment Providers
            // =============================================================================

            var providerFactory = app.Services.GetRequiredService<PaymentProviderFactory>();
            var stripeProvider = app.Services.GetRequiredService<StripePaymentProvider>();
            var paypalProvider = app.Services.GetRequiredService<PayPalPaymentProvider>();
            providerFactory.RegisterProvider(stripeProvider);
            providerFactory.RegisterProvider(paypalProvider);

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
