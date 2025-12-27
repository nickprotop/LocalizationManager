using Octokit;
using Polly;
using Polly.Retry;

namespace LrmCloud.Api.Helpers;

/// <summary>
/// Retry policy for GitHub API calls to handle transient failures.
/// </summary>
public static class GitHubRetryPolicy
{
    private static readonly ResiliencePipeline Pipeline;

    static GitHubRetryPolicy()
    {
        Pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<ApiException>(ex => IsTransientError(ex))
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build();
    }

    /// <summary>
    /// Executes an action with retry logic for transient GitHub API failures.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(Func<CancellationToken, ValueTask<T>> action, ILogger? logger = null)
    {
        return await Pipeline.ExecuteAsync(async token =>
        {
            try
            {
                return await action(token);
            }
            catch (Exception ex) when (IsTransientError(ex))
            {
                logger?.LogWarning(ex, "Transient GitHub API error, will retry");
                throw;
            }
        });
    }

    /// <summary>
    /// Executes an action with retry logic for transient GitHub API failures.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> action, ILogger? logger = null)
    {
        return await ExecuteAsync(async _ => await action(), logger);
    }

    /// <summary>
    /// Executes an action with retry logic for transient GitHub API failures.
    /// </summary>
    public static async Task ExecuteAsync(Func<Task> action, ILogger? logger = null)
    {
        await ExecuteAsync(async () =>
        {
            await action();
            return true;
        }, logger);
    }

    /// <summary>
    /// Determines if an exception is a transient error that should be retried.
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        return ex switch
        {
            ApiException apiEx => IsTransientStatusCode(apiEx.StatusCode),
            HttpRequestException => true,
            TaskCanceledException => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if a status code indicates a transient error.
    /// </summary>
    private static bool IsTransientStatusCode(System.Net.HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.RequestTimeout => true,        // 408
            System.Net.HttpStatusCode.TooManyRequests => true,       // 429
            System.Net.HttpStatusCode.InternalServerError => true,   // 500
            System.Net.HttpStatusCode.BadGateway => true,            // 502
            System.Net.HttpStatusCode.ServiceUnavailable => true,    // 503
            System.Net.HttpStatusCode.GatewayTimeout => true,        // 504
            _ => false
        };
    }
}
