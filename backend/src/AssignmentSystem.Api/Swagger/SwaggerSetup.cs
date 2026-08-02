using Microsoft.OpenApi.Models;

namespace AssignmentSystem.Api.Swagger;

/// <summary>
/// Centralized Swashbuckle configuration: registers the JWT bearer security scheme
/// so the Swagger UI can authenticate requests during development.
/// </summary>
public static class SwaggerSetup
{
    public const string BearerScheme = "Bearer";

    public static void AddJwtBearerSecurity(Swashbuckle.AspNetCore.SwaggerGen.SwaggerGenOptions options)
    {
        options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Enter 'Bearer' followed by a space and your JWT access token.",
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = BearerScheme },
                },
                Array.Empty<string>()
            },
        });
    }
}
