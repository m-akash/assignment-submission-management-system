using System.Net;
using AssignmentSystem.Api.Common;
using AssignmentSystem.Api.Middleware;
using AssignmentSystem.Shared.Common;
using Microsoft.AspNetCore.Mvc;

namespace AssignmentSystem.Api.Common;

/// <summary>
/// Maps <see cref="Result{T}"/> to a consistent action result. Success → 200 with the
/// <see cref="ApiResponse{T}"/> envelope; failure → ProblemDetails (RFC 7807) with the
/// HTTP status derived from <see cref="ErrorType"/>. The single place this mapping lives.
/// </summary>
public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller) =>
        result.IsSuccess ? Ok(controller, result.Value) : Failure(controller, result.Error);

    public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller, Func<T, object> projection) =>
        result.IsSuccess ? Ok(controller, projection(result.Value!)) : Failure(controller, result.Error);

    public static IActionResult ToActionResult(this Result result, ControllerBase controller, string? successMessage = null) =>
        result.IsSuccess ? Ok(controller, default(object), successMessage) : Failure(controller, result.Error);

    private static OkObjectResult Ok(ControllerBase controller, object? data, string? message = null)
    {
        var payload = new ApiResponse<object> { Success = true, Data = data, Message = message };
        return controller.Ok(payload);
    }

    public static ObjectResult PagedOk<T>(ControllerBase controller, PageResult<T> page, Func<T, object>? projection = null)
    {
        var items = projection is null
            ? page.Items.Cast<object>().ToList()
            : page.Items.Select(projection).ToList();

        var payload = new ApiResponse<List<object>>
        {
            Success = true,
            Data = items,
            Pagination = new PaginationMetaData
            {
                Page = page.Page,
                PageSize = page.PageSize,
                Total = page.Total,
                TotalPages = page.TotalPages,
            },
        };
        return controller.Ok(payload);
    }

    private static ObjectResult Failure(ControllerBase controller, Error error)
    {
        var (status, title) = Map(error.Type);
        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Type = $"https://httpstatuses.io/{(int)status}",
            Detail = error.Message,
            Instance = controller.HttpContext.Request.Path,
        };

        // Surface the correlation id for client-side traceability.
        if (controller.HttpContext.Items.TryGetValue(CorrelationIdContext.Key, out var cid) && cid is not null)
        {
            problem.Extensions["traceId"] = cid.ToString();
        }

        if (error.Code is { Length: > 0 })
        {
            problem.Extensions["code"] = error.Code;
        }

        if (error.ValidationErrors is { Count: > 0 })
        {
            problem.Extensions["errors"] = error.ValidationErrors;
        }

        return new ObjectResult(problem)
        {
            StatusCode = (int)status,
            ContentTypes = { "application/problem+json" },
        };
    }

    private static (HttpStatusCode Status, string Title) Map(ErrorType type) => type switch
    {
        ErrorType.Validation => (HttpStatusCode.UnprocessableEntity, "Validation failed."),
        ErrorType.NotFound => (HttpStatusCode.NotFound, "Resource not found."),
        ErrorType.Unauthorized => (HttpStatusCode.Unauthorized, "Authentication required."),
        ErrorType.Forbidden => (HttpStatusCode.Forbidden, "Access denied."),
        ErrorType.Conflict => (HttpStatusCode.Conflict, "Conflict."),
        _ => (HttpStatusCode.BadRequest, "Bad request."),
    };
}
