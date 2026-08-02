using System.Net;
using System.Text.Json;
using AssignmentSystem.Api.Common;
using AssignmentSystem.Api.Middleware;
using AssignmentSystem.Domain.Common;
using AssignmentSystem.Shared.Common;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace AssignmentSystem.Api.Middleware;

/// <summary>
/// Global exception→ProblemDetails (RFC 7807) mapping. Guarantees no raw stack trace
/// escapes to the client, every failure is correlated + logged, and each exception kind
/// maps to the correct HTTP status. Expected domain failures use the Result pattern; this
/// catches everything that slipped through.
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

        var (status, title, message) = Map(ex);

        Log.Error(ex, "Unhandled exception on {Method} {Path}; Status={Status}; CorrelationId={CorrelationId}",
            context.Request.Method, context.Request.Path, (int)status, correlationId);

        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetailsPayload
        {
            Status = (int)status,
            Title = title,
            Type = $"https://httpstatuses.io/{(int)status}",
            Detail = _env.IsDevelopment() ? ex.Message : message,
            Instance = context.Request.Path,
        };
        if (correlationId is not null)
        {
            problem.Extensions["traceId"] = correlationId;
        }

        // Surface validation error details.
        if (ex is ValidationException validation)
        {
            var errors = validation.Errors
                .GroupBy(e => e.PropertyName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
            problem.Extensions["errors"] = errors;
        }

        await JsonSerializer.SerializeAsync(context.Response.Body, problem, JsonOptions);
    }

    private static (HttpStatusCode Status, string Title, string Message) Map(Exception ex) => ex switch
    {
        ValidationException => (HttpStatusCode.UnprocessableEntity, "Validation failed.", "One or more validation errors occurred."),
        DomainException => (HttpStatusCode.BadRequest, "Domain rule violated.", ex.Message),
        UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Authentication required.", "Authentication is required."),
        DbUpdateConcurrencyException => (HttpStatusCode.Conflict, "Conflict.", "The record was modified by another user. Please refresh and retry."),
        DbUpdateException => (HttpStatusCode.Conflict, "Conflict.", "A data conflict occurred."),
        OperationCanceledException => (HttpStatusCode.BadRequest, "Request cancelled.", "The request was cancelled."),
        _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", "An internal server error occurred. Please try again later."),
    };
}

internal sealed class ProblemDetailsPayload
{
    public int Status { get; init; }
    public string? Title { get; init; }
    public string? Type { get; init; }
    public string? Detail { get; init; }
    public string? Instance { get; init; }
    public Dictionary<string, object?> Extensions { get; } = new();
}
