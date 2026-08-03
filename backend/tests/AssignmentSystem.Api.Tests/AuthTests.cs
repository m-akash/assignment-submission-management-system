using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using Xunit;
using FluentAssertions;

namespace AssignmentSystem.Api.Tests;

public class AuthTests : IntegrationTestBase
{
    public AuthTests(ApiFactory api) : base(api) { }

    [Fact]
    public async Task Login_WithValidCredentials_ShouldReturnOkAndToken()
    {
        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(DbSeeder.AdminEmail, DbSeeder.DefaultPassword));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<AuthResponseBody>>(JsonOptions);
        payload.Should().NotBeNull();
        payload!.Success.Should().BeTrue();
        payload.Data.Should().NotBeNull();
        payload.Data!.Email.Should().Be(DbSeeder.AdminEmail);
        payload.Data.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ShouldReturnUnauthorized()
    {
        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(DbSeeder.AdminEmail, "WrongPassword"));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
