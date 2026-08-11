using System.Text;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using AssignmentSystem.Api.Authentication;
using AssignmentSystem.Api.Common;
using AssignmentSystem.Api.Middleware;
using AssignmentSystem.Api.Swagger;
using AssignmentSystem.Application;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Infrastructure;
using AssignmentSystem.Infrastructure.Persistence;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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

    // ── Rate limiting (credential endpoints) ──────────────────────────────────
    // Partitioned by client address, not globally: a shared limit would let one noisy
    // caller lock the whole school out of signing in.
    //
    // This bounds how fast anyone can *try*; ApplicationUser's lockout bounds how many
    // times a single account can be guessed at in total. Neither alone is enough — a
    // distributed guess slips under a per-IP limit, and a per-account lock does nothing
    // about someone spraying one password across every address in the directory.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.AddPolicy(RateLimitPolicies.Credentials, httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimiting:CredentialsPerMinute", 10),
                    Window = TimeSpan.FromMinutes(1),
                    // No queue: a caller over the limit is told so immediately rather than
                    // held open, which would tie up connections during exactly the burst
                    // this exists to survive.
                    QueueLimit = 0,
                }));

        options.OnRejected = async (context, ct) =>
        {
            var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                ? (int)retryAfter.TotalSeconds
                : 60;
            context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(
                System.Globalization.CultureInfo.InvariantCulture);
            // Content type passed to WriteAsJsonAsync rather than set beforehand — it
            // overwrites the header with application/json otherwise, and a client branching
            // on problem+json would stop recognising this as an error it can read.
            await context.HttpContext.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests.",
                    Type = "https://httpstatuses.io/429",
                    Detail = "Too many attempts. Please wait a moment and try again.",
                    Instance = context.HttpContext.Request.Path,
                },
                options: null,
                contentType: "application/problem+json",
                cancellationToken: ct);
        };
    });

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
    var maxUploadBytes = builder.Configuration.GetValue("FileStorage:MaxBytes", 2L * 1024 * 1024);
    builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        options.MultipartBodyLengthLimit = maxUploadBytes + MultipartFramingHeadroom);

    // ── MVC / JSON ────────────────────────────────────────────────────────────
    // No validation filter: request bodies are mapped to commands, and the Application
    // layer's ValidationDecorator validates those. Validating here as well would mean two
    // sets of rules for one request — which is exactly what this replaced.
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

    // Always on in development. Elsewhere it stays off unless Swagger:Enabled is set,
    // since the UI publishes the whole API surface — worth opting into deliberately.
    if (app.Environment.IsDevelopment() || builder.Configuration.GetValue("Swagger:Enabled", false))
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ExceptionHandlingMiddleware>();

    app.UseRateLimiter();

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
