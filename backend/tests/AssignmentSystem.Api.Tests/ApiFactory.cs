using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using AssignmentSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Hosts the API against a real PostgreSQL container for the whole test assembly.
///
/// Shared deliberately: spinning a container per test method costs ~10s each, and the
/// point of these tests is to exercise real FK/unique/concurrency behaviour, which one
/// database serves just as well. Tests therefore create their own fixtures (with unique
/// emails and codes) rather than asserting on global counts.
/// </summary>
public sealed class ApiFactory : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder()
        .WithDatabase("assignment_system_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    private WebApplicationFactory<Program> _factory = null!;

    /// <summary>Throwaway upload root so file tests never touch the developer's disk layout.</summary>
    public string FileStorageRoot { get; } =
        Path.Combine(Path.GetTempPath(), "asm-api-tests", Guid.NewGuid().ToString("N"));

    public IServiceProvider Services => _factory.Services;

    public HttpClient CreateClient() => _factory.CreateClient();

    /// <summary>A client that does not persist cookies, for driving the refresh cookie by hand.</summary>
    public HttpClient CreateCookielessClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    /// <summary>Opens a DbContext outside the request pipeline, for persistence-level assertions.</summary>
    public AsyncServiceScope CreateScope() => _factory.Services.CreateAsyncScope();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        Directory.CreateDirectory(FileStorageRoot);

        // Configuration overrides only — no service surgery. The app reads both keys
        // through IConfiguration, so the real composition root stays under test.
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", _database.GetConnectionString());
            builder.UseSetting("FileStorage:Root", FileStorageRoot);

            // The outbox dispatcher is off so notification tests are deterministic: a timer
            // sweeping in the background would flip rows from Pending to Sent underneath an
            // assertion. Draining is still exercised — through the explicit dispatch endpoint.
            builder.UseSetting("Email:EnableDispatcher", "false");
            builder.UseSetting("Email:AppBaseUrl", "http://localhost:3000");

            // Every test in the assembly shares one host, and TestServer reports no remote
            // address — so all of them land in a single rate-limit partition. Raised well
            // clear of the suite's total sign-ins so the limiter cannot make an unrelated
            // test flake. RateLimitingTests overrides it back down for its own host.
            builder.UseSetting("RateLimiting:CredentialsPerMinute", "10000");
        });

        // Creating a client boots the host, which migrates and seeds on startup.
        using var warmup = _factory.CreateClient();
        await using var scope = CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.CanConnectAsync();
    }

    /// <summary>
    /// A second host over the same database, with the credential rate limit set low enough
    /// to trip on purpose. The shared host runs with the limit effectively disabled, so a
    /// test that wants to prove the limiter works needs its own — and reusing the container
    /// keeps that to milliseconds rather than another ten seconds of Postgres startup.
    /// </summary>
    public WebApplicationFactory<Program> CreateHostWithRateLimit(int permitsPerMinute) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Default", _database.GetConnectionString());
            builder.UseSetting("FileStorage:Root", FileStorageRoot);
            builder.UseSetting("Email:EnableDispatcher", "false");
            builder.UseSetting("Email:AppBaseUrl", "http://localhost:3000");

            // The schema is already migrated and seeded by the shared host.
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting(
                "RateLimiting:CredentialsPerMinute",
                permitsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _database.DisposeAsync();

        try
        {
            if (Directory.Exists(FileStorageRoot))
            {
                Directory.Delete(FileStorageRoot, recursive: true);
            }
        }
        catch (IOException)
        {
            // A locked upload handle must not fail the run.
        }
    }
}

/// <summary>Binds every integration test class to the one shared API + database.</summary>
[CollectionDefinition(ApiCollection.Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
