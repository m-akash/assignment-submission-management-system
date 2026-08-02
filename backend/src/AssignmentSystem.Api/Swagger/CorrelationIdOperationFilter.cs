using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AssignmentSystem.Api.Swagger;

/// <summary>
/// Adds the optional X-Correlation-Id header parameter to every documented operation
/// so consumers know they can supply one for trace correlation.
/// </summary>
public sealed class CorrelationIdOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        if (operation.Parameters.All(p => p.Name != Middleware.CorrelationIdMiddleware.HeaderName))
        {
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = Middleware.CorrelationIdMiddleware.HeaderName,
                In = ParameterLocation.Header,
                Required = false,
                Description = "Optional correlation id for request tracing.",
                Schema = new OpenApiSchema { Type = "string" },
            });
        }
    }
}
