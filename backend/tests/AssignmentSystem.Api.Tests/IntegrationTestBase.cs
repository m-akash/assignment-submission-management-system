using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Infrastructure.Persistence;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

namespace AssignmentSystem.Api.Tests;

public class IntegrationTestBase : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    protected readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder()
        .WithDatabase("assignment_system_test")
        .WithUsername("test_user")
        .WithPassword("test_pass")
        .Build();

    protected WebApplicationFactory<Program> _factory = null!;
    protected HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Remove current DbContext
                    services.RemoveAll<DbContextOptions<AppDbContext>>();

                    // Add DB pointing to Testcontainers
                    services.AddDbContext<AppDbContext>(options =>
                    {
                        options.UseNpgsql(_dbContainer.GetConnectionString());
                    });
                });
            });

        _client = _factory.CreateClient();

        // Migrate and seed database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        
        var seeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
        await seeder.SeedAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _dbContainer.StopAsync();
    }

    protected async Task AuthenticateAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<AuthResponseBody>>(JsonOptions);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload!.Data!.AccessToken);
    }

    protected sealed class ApiResponseEnvelope<T>
    {
        public bool Success { get; set; }
        public T? Data { get; set; }
    }
}
