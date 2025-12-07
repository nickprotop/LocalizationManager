namespace LrmCloud.Api.Middleware;

using LrmCloud.Shared.Api;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

/// <summary>
/// Global exception handler that:
/// - Logs all exceptions with Serilog
/// - Returns ProblemDetails with our error codes
/// - Hides exception details in production
/// </summary>
public static class GlobalExceptionHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void ConfigureExceptionHandler(this IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseExceptionHandler(appBuilder =>
        {
            appBuilder.Run(async context =>
            {
                var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
                var exception = exceptionFeature?.Error;
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();

                // Log the exception
                logger.LogError(exception,
                    "Unhandled exception on {Method} {Path}",
                    context.Request.Method,
                    context.Request.Path);

                // Determine status code based on exception type
                var (statusCode, errorCode, title) = MapException(exception);

                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";

                var problemDetails = new ProblemDetails
                {
                    Type = $"https://lrm.cloud/errors/{errorCode.ToLowerInvariant().Replace('_', '-')}",
                    Title = title,
                    Status = statusCode,
                    Instance = context.Request.Path,
                    Detail = env.IsDevelopment() 
                        ? exception?.Message 
                        : "An error occurred processing your request."
                };

                // Add timestamp
                problemDetails.Extensions["timestamp"] = DateTime.UtcNow;
                problemDetails.Extensions["errorCode"] = errorCode;

                // Add trace ID for correlation
                problemDetails.Extensions["traceId"] = context.TraceIdentifier;

                // Include stack trace only in development
                if (env.IsDevelopment() && exception != null)
                {
                    problemDetails.Extensions["exception"] = new
                    {
                        type = exception.GetType().Name,
                        message = exception.Message,
                        stackTrace = exception.StackTrace?.Split('\n').Take(10).ToArray()
                    };
                }

                await context.Response.WriteAsJsonAsync(problemDetails, JsonOptions);
            });
        });
    }

    private static (int StatusCode, string ErrorCode, string Title) MapException(Exception? exception)
    {
        return exception switch
        {
            ArgumentNullException => (400, ErrorCodes.VAL_REQUIRED_FIELD, "Bad Request"),
            ArgumentException => (400, ErrorCodes.VAL_INVALID_INPUT, "Bad Request"),
            UnauthorizedAccessException => (401, ErrorCodes.AUTH_UNAUTHORIZED, "Unauthorized"),
            KeyNotFoundException => (404, ErrorCodes.RES_NOT_FOUND, "Not Found"),
            InvalidOperationException => (400, ErrorCodes.VAL_INVALID_INPUT, "Bad Request"),
            TimeoutException => (503, ErrorCodes.SRV_SERVICE_UNAVAILABLE, "Service Unavailable"),
            OperationCanceledException => (499, ErrorCodes.SRV_SERVICE_UNAVAILABLE, "Client Closed Request"),
            _ => (500, ErrorCodes.SRV_INTERNAL_ERROR, "Internal Server Error")
        };
    }
}
