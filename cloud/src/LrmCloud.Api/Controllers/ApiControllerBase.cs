namespace LrmCloud.Api.Controllers;

using LrmCloud.Shared.Api;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Base controller with helper methods for standardized API responses.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    // ==========================================================================
    // Success Responses
    // ==========================================================================

    /// <summary>
    /// Returns 200 OK with data
    /// </summary>
    protected ActionResult<ApiResponse<T>> Success<T>(T data, ApiMeta? meta = null)
    {
        return Ok(new ApiResponse<T>
        {
            Data = data,
            Meta = meta ?? ApiMetaExtensions.Now()
        });
    }

    /// <summary>
    /// Returns 200 OK with a message (no data)
    /// </summary>
    protected ActionResult<ApiResponse> Success(string message)
    {
        return Ok(new ApiResponse
        {
            Message = message,
            Meta = ApiMetaExtensions.Now()
        });
    }

    /// <summary>
    /// Returns 201 Created with data and location header
    /// </summary>
    protected ActionResult<ApiResponse<T>> Created<T>(string actionName, object routeValues, T data)
    {
        return CreatedAtAction(actionName, routeValues, new ApiResponse<T>
        {
            Data = data,
            Meta = ApiMetaExtensions.Now()
        });
    }

    /// <summary>
    /// Returns 204 No Content
    /// </summary>
    protected new ActionResult NoContent()
    {
        return base.NoContent();
    }

    // ==========================================================================
    // Paginated Responses
    // ==========================================================================

    /// <summary>
    /// Returns 200 OK with paginated data
    /// </summary>
    protected ActionResult<ApiResponse<List<T>>> Paginated<T>(
        List<T> items,
        int page,
        int pageSize,
        int totalCount)
    {
        return Ok(new ApiResponse<List<T>>
        {
            Data = items,
            Meta = ApiMetaExtensions.ForPage(page, pageSize, totalCount)
        });
    }

    // ==========================================================================
    // Error Responses (using ProblemDetails)
    // ==========================================================================

    /// <summary>
    /// Returns 400 Bad Request with ProblemDetails
    /// </summary>
    protected ActionResult BadRequest(string errorCode, string detail)
    {
        return Problem(
            title: "Bad Request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest,
            type: $"https://lrm.cloud/errors/{errorCode.ToLowerInvariant().Replace('_', '-')}"
        );
    }

    /// <summary>
    /// Returns 401 Unauthorized with ProblemDetails
    /// </summary>
    protected ActionResult Unauthorized(string errorCode, string detail)
    {
        return Problem(
            title: "Unauthorized",
            detail: detail,
            statusCode: StatusCodes.Status401Unauthorized,
            type: $"https://lrm.cloud/errors/{errorCode.ToLowerInvariant().Replace('_', '-')}"
        );
    }

    /// <summary>
    /// Returns 403 Forbidden with ProblemDetails
    /// </summary>
    protected ActionResult Forbidden(string errorCode, string detail)
    {
        return Problem(
            title: "Forbidden",
            detail: detail,
            statusCode: StatusCodes.Status403Forbidden,
            type: $"https://lrm.cloud/errors/{errorCode.ToLowerInvariant().Replace('_', '-')}"
        );
    }

    /// <summary>
    /// Returns 404 Not Found with ProblemDetails
    /// </summary>
    protected ActionResult NotFound(string errorCode, string detail)
    {
        return Problem(
            title: "Not Found",
            detail: detail,
            statusCode: StatusCodes.Status404NotFound,
            type: $"https://lrm.cloud/errors/{errorCode.ToLowerInvariant().Replace('_', '-')}"
        );
    }

    /// <summary>
    /// Returns 409 Conflict with ConflictResponse (for sync conflicts)
    /// </summary>
    protected ActionResult Conflict(List<ResourceConflict> conflicts)
    {
        return base.Conflict(new ConflictResponse { Conflicts = conflicts });
    }

    /// <summary>
    /// Returns 409 Conflict with ProblemDetails (for other conflicts)
    /// </summary>
    protected ActionResult Conflict(string errorCode, string detail)
    {
        return Problem(
            title: "Conflict",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict,
            type: $"https://lrm.cloud/errors/{errorCode.ToLowerInvariant().Replace('_', '-')}"
        );
    }

    /// <summary>
    /// Returns 429 Too Many Requests with ProblemDetails
    /// </summary>
    protected ActionResult TooManyRequests(string detail, int? retryAfterSeconds = null)
    {
        if (retryAfterSeconds.HasValue)
        {
            Response.Headers.Append("Retry-After", retryAfterSeconds.Value.ToString());
        }

        return Problem(
            title: "Too Many Requests",
            detail: detail,
            statusCode: StatusCodes.Status429TooManyRequests,
            type: "https://lrm.cloud/errors/srv-rate-limited"
        );
    }
}
