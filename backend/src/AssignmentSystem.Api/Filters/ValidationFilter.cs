using System.Net;
using System.Text.Json;
using AssignmentSystem.Api.Common;
using AssignmentSystem.Api.Middleware;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AssignmentSystem.Api.Filters;

/// <summary>
/// Runs the matching FluentValidation validator for a bound command argument and short-
/// circuits with a 422 ProblemDetails response on failure. Attached globally so every
/// endpoint gets server-side validation automatically. This is the source of truth for
/// request-shape validation — business rules still live in the domain/handlers.
/// </summary>
public sealed class ValidationFilter : IAsyncActionFilter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IServiceProvider _provider;

    public ValidationFilter(IServiceProvider provider) => _provider = provider;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var (key, value) in context.ActionArguments)
        {
            if (value is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(value.GetType());
            var validator = _provider.GetService(validatorType) as IValidator;
            if (validator is null)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(value);
            var result = await validator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);
            if (!result.IsValid)
            {
                context.Result = BuildProblemDetails(context, result);
                return;
            }
        }

        await next();
    }

    private static ObjectResult BuildProblemDetails(ActionExecutingContext context, FluentValidation.Results.ValidationResult result)
    {
        var errors = result.Errors
            .GroupBy(e => e.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        var correlationId = context.HttpContext.Items.TryGetValue(CorrelationIdContext.Key, out var cid)
            ? cid?.ToString()
            : null;

        var problem = new ProblemDetailsPayload
        {
            Status = (int)HttpStatusCode.UnprocessableEntity,
            Title = "Validation failed.",
            Type = "https://httpstatuses.io/422",
            Detail = "One or more validation errors occurred.",
            Instance = context.HttpContext.Request.Path,
        };
        problem.Extensions["errors"] = errors;
        if (correlationId is not null)
        {
            problem.Extensions["traceId"] = correlationId;
        }

        return new ObjectResult(problem)
        {
            StatusCode = (int)HttpStatusCode.UnprocessableEntity,
            ContentTypes = { "application/problem+json" },
        };
    }
}

internal sealed class ProblemDetailsPayload
{
    public int? Status { get; init; }
    public string? Title { get; init; }
    public string? Type { get; init; }
    public string? Detail { get; init; }
    public string? Instance { get; init; }
    public Dictionary<string, object?> Extensions { get; } = new();
}
