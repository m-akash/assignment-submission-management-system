using System.Text;
using System.Text.Json.Serialization;
using AssignmentSystem.Api.Authentication;
using AssignmentSystem.Api.Middleware;
using AssignmentSystem.Api.Swagger;
using AssignmentSystem.Application;
using AssignmentSystem.Application.Abstractions;
using FluentValidation;
using AssignmentSystem.Infrastructure;
using AssignmentSystem.Infrastructure.Persistence;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
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

    // ── Layer services ────────────────────────────────────────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    // Register request-body validators that live in the Api assembly.
    builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly);

    // ── Current user (per-request, from HttpContext claims) ───────────────────
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    // ── Authentication (JWT bearer) ───────────────────────────────────────────
    var jwtKey = builder.Configuration["Jwt:Key"]!;
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.FromSeconds(30),
            };
        });

    builder.Services.AddAuthorization();

    // ── CORS (frontend origin allowlist) ──────────────────────────────────────
    var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials());
    });

    // ── Multipart body limit ──────────────────────────────────────────────────
    // Driven by the same FileStorage:MaxBytes the upload policy enforces, so the two
    // cannot drift. The headroom covers multipart framing (boundaries and part headers)
    // so a file a little over the limit is refused by the policy — with a clear 422 and a
    // stated maximum — instead of being cut off mid-body by the server.
    const long MultipartFramingHeadroom = 8 * 1024;
    var maxUploadBytes = builder.Configuration.GetValue("FileStorage:MaxBytes", 10L * 1024 * 1024);
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        options.MultipartBodyLengthLimit = maxUploadBytes + MultipartFramingHeadroom);

    // ── MVC / JSON ────────────────────────────────────────────────────────────
    builder.Services.AddControllers(o => o.Filters.Add<AssignmentSystem.Api.Filters.ValidationFilter>())
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

    // ── Notification outbox dispatcher ────────────────────────────────────────
    // Opt-out via Email:EnableDispatcher so integration tests can assert on queued rows
    // without a timer racing them, and so a second instance can be run purely as an API.
    if (builder.Configuration.GetValue("Email:EnableDispatcher", true))
    {
        builder.Services.AddHostedService<AssignmentSystem.Api.BackgroundServices.NotificationDispatcherService>();
    }

    // ── Cross-cutting ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>();

    var app = builder.Build();

    // ── Auto-migrate + seed on startup (configurable) ─────────────────────────
    if (builder.Configuration.GetValue("Database:AutoMigrate", true))
    {
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();

            if (builder.Configuration.GetValue("Database:SeedOnStartup", true))
            {
                var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
                await seeder.SeedAsync();
            }
        }
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseAuthentication();
    app.UseAuthorization();

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
