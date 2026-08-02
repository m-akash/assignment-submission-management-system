using AssignmentSystem.Api.Common;

namespace AssignmentSystem.Api.Middleware;

/// <summary>
/// Ensures every request carries a correlation id (incoming header or a fresh GUID)
/// and echoes it back on the response. Threaded into Serilog scopes so a single
/// request's logs are traceable end-to-end.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var incoming) && !string.IsNullOrWhiteSpace(incoming)
            ? incoming.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[CorrelationIdContext.Key] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IServiceCollection AddCorrelationIdMiddleware(this IServiceCollection services) => services;
}
