using System.Net;
using System.Text.Json;
using AssignmentSystem.Api.Common;
using Serilog;

namespace AssignmentSystem.Api.Middleware;

/// <summary>
/// Global exception→ProblemDetails (RFC 7807) mapping. Expanded fully in Phase 2
/// with mappings for ValidationException, UnauthorizedAccessException,
/// DbUpdateConcurrencyException, DomainException, etc. For now it guarantees no
/// raw stack trace ever escapes to the client and that every failure is logged.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, IHostEnvironment env)
    {
        _next = next;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteProblemDetailsAsync(context, ex);
        }
    }

    private async Task WriteProblemDetailsAsync(HttpContext context, Exception ex)
    {
        var correlationId = context.Items.TryGetValue(CorrelationIdContext.Key, out var cid)
            ? cid?.ToString()
            : null;

        Log.Error(ex, "Unhandled exception on {Method} {Path}; CorrelationId={CorrelationId}",
            context.Request.Method, context.Request.Path, correlationId);

        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Type = "https://httpstatuses.io/500",
            Detail = _env.IsDevelopment() ? ex.ToString() : "An internal server error occurred. Please try again later.",
            Instance = context.Request.Path,
        };
        if (correlationId is not null)
        {
            problem.Extensions["traceId"] = correlationId;
        }

        await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonOptions);
    }
}

// Lightweight ProblemDetails carrier (avoids pulling in extra refs at this stage).
internal sealed class ProblemDetails
{
    public int? Status { get; init; }
    public string? Title { get; init; }
    public string? Type { get; init; }
    public string? Detail { get; init; }
    public string? Instance { get; init; }
    public Dictionary<string, object?> Extensions { get; } = new();
}
