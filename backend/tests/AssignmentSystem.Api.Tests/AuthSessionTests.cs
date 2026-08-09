using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence.Seed;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Covers the session lifecycle the browser depends on: identity lookup via
/// <c>/auth/me</c>, cookie-based refresh, logout revocation, and refresh-token reuse
/// detection (rule X8).
/// </summary>
public class AuthSessionTests : IntegrationTestBase
{
    public AuthSessionTests(ApiFactory api) : base(api) { }

    [Fact]
    public async Task GetMe_WithoutToken_ShouldReturnUnauthorized()
    {
        var response = await Client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_AsStudent_ShouldReturnProfileWithClassMembership()
    {
        await AuthenticateAsync(DbSeeder.StudentEmail, DbSeeder.DefaultPassword);

        var response = await Client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<CurrentUser>>(JsonOptions);

        payload!.Data!.Email.Should().Be(DbSeeder.StudentEmail);
        payload.Data.Role.Should().Be(Role.Student);

        // The login body omits these; the frontend reads them from here. Membership is a
        // list now, because a student can be enrolled in more than one class.
        payload.Data.Classes.Should().NotBeNullOrEmpty();
        payload.Data.Classes[0].ClassLevel.Should().BeInRange(1, 12);
        payload.Data.Classes[0].ClassSection.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetMe_AsAdmin_ShouldReturnNoClassMembership()
    {
        await AuthenticateAsync(DbSeeder.AdminEmail, DbSeeder.DefaultPassword);

        var response = await Client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<CurrentUser>>(JsonOptions);

        payload!.Data!.Role.Should().Be(Role.Admin);
        payload.Data.Classes.Should().BeEmpty();
    }

    [Fact]
    public async Task Login_ShouldIssueRefreshCookie_NotExposeTokenInBody()
    {
        using var client = CreateCookielessClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DbSeeder.TeacherEmail, DbSeeder.DefaultPassword));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ReadRefreshCookie(response).Should().NotBeNullOrEmpty();

        // The refresh token must live only in the httpOnly cookie.
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("refreshToken");
    }

    [Fact]
    public async Task Refresh_WithCookieOnly_ShouldIssueNewAccessToken()
    {
        // The cookie-persisting client mirrors what a browser does: no bearer token
        // is attached, the session is restored purely from the cookie.
        var login = await Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DbSeeder.StudentEmail, DbSeeder.DefaultPassword));
        login.EnsureSuccessStatusCode();

        var response = await Client.PostAsync("/api/v1/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponseEnvelope<AuthResponseBody>>(JsonOptions);
        payload!.Data!.AccessToken.Should().NotBeNullOrEmpty();
        payload.Data.Email.Should().Be(DbSeeder.StudentEmail);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_ShouldReturnUnauthorized()
    {
        using var client = CreateCookielessClient();

        var response = await client.PostAsync("/api/v1/auth/refresh", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_ShouldRevokeRefreshToken()
    {
        var login = await Client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DbSeeder.TeacherEmail, DbSeeder.DefaultPassword));
        login.EnsureSuccessStatusCode();

        // Anonymous on purpose: an expired access token must not block logging out.
        var logout = await Client.PostAsync("/api/v1/auth/logout", null);
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refresh = await Client.PostAsync("/api/v1/auth/refresh", null);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>Rule X8 — replaying a rotated token revokes the entire token family.</summary>
    [Fact]
    public async Task Refresh_WhenRotatedTokenIsReplayed_ShouldRevokeWholeFamily()
    {
        using var client = CreateCookielessClient();

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(DbSeeder.AdminEmail, DbSeeder.DefaultPassword));
        login.EnsureSuccessStatusCode();
        var firstToken = ReadRefreshCookie(login);
        firstToken.Should().NotBeNullOrEmpty();

        var rotated = await RefreshWithCookieAsync(client, firstToken!);
        rotated.StatusCode.Should().Be(HttpStatusCode.OK);
        var secondToken = ReadRefreshCookie(rotated);
        secondToken.Should().NotBeNullOrEmpty().And.NotBe(firstToken);

        // Replaying the already-rotated token is the theft signal.
        var replay = await RefreshWithCookieAsync(client, firstToken!);
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // ...and it takes the descendant token down with it.
        var afterFamilyRevoke = await RefreshWithCookieAsync(client, secondToken!);
        afterFamilyRevoke.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<HttpResponseMessage> RefreshWithCookieAsync(HttpClient client, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"{AuthCookieName}={refreshToken}");
        return await client.SendAsync(request);
    }

    /// <summary>Mirror of the server's <c>UserDto</c> for the fields this suite asserts on.</summary>
    private sealed record CurrentUser(
        Guid Id,
        string Email,
        string FullName,
        Role Role,
        bool IsActive,
        List<EnrolledClass> Classes);

    private sealed record EnrolledClass(Guid ClassId, int ClassLevel, string? ClassSection);
}
