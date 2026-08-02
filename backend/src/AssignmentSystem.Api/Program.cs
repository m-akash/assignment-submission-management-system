using System.Text.Json.Serialization;
using AssignmentSystem.Api.Middleware;
using AssignmentSystem.Api.Swagger;
using Serilog;

// ── Serilog bootstrap (early, before host build) ─────────────────────────────
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog (read sinks/enrichers from appsettings) ───────────────────────
    builder.Host.UseSerilog((context, services, cfg) => cfg
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services));

    // ── MVC / JSON ────────────────────────────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    // ── Swagger / OpenAPI ─────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.OperationFilter<CorrelationIdOperationFilter>();
        SwaggerSetup.AddJwtBearerSecurity(options);
    });

    // ── Cross-cutting ─────────────────────────────────────────────────────────
    builder.Services.AddCorrelationIdMiddleware();
    builder.Services.AddHealthChecks();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.MapControllers();
    app.MapHealthChecks("/health");

    Log.Information("AssignmentSystem API starting in {Environment} environment", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposed as public partial so WebApplicationFactory<Program> in Api.Tests can reference it.
public partial class Program;
