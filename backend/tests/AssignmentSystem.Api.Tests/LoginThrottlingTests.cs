using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// Per-account login throttling. Distinct from the per-IP rate limit: this is what stops a
/// slow, distributed guess at one account, which no request-rate cap would ever notice.
/// </summary>
public sealed class LoginThrottlingTests : IntegrationTestBase
{
    // Matches AuthOptions.MaxFailedLoginAttempts.
    private const int MaxAttempts = 5;

    public LoginThrottlingTests(ApiFactory api) : base(api) { }

    private static Task<HttpResponseMessage> AttemptAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));

    private async Task<string> ProvisionUserAsync(string label)
    {
        using var admin = await SignInAsAdminAsync();
        var tag = $"{label}{Guid.NewGuid():N}"[..12];
        var email = $"{tag}@throttle.local";

        await PostAsync<UserRef>(admin, "/api/v1/users",
            new CreateUserRequest(email, $"Throttle {tag}", TestPassword, Role.Teacher, null));

        return email;
    }

    [Fact]
    public async Task WrongPassword_IncrementsTheFailureCountOnTheAccount()
    {
        var email = await ProvisionUserAsync("count");
        using var client = Api.CreateClient();

        await AttemptAsync(client, email, "definitely-wrong");
        await AttemptAsync(client, email, "definitely-wrong");

        await using var scope = Api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email.Value == email);

        user.FailedLoginAttempts.Should().Be(2);
        user.LockoutEndUtc.Should().BeNull("two attempts is well under the threshold");
    }

    [Fact]
    public async Task TheCorrectPassword_ClearsAnyAccumulatedFailures()
    {
        var email = await ProvisionUserAsync("clear");
        using var client = Api.CreateClient();

        await AttemptAsync(client, email, "wrong-once");
        await AttemptAsync(client, email, "wrong-twice");
        var success = await AttemptAsync(client, email, TestPassword);
        success.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var scope = Api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email.Value == email);

        user.FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task ReachingTheThreshold_LocksTheAccount()
    {
        var email = await ProvisionUserAsync("lock");
        using var client = Api.CreateClient();

        for (var i = 0; i < MaxAttempts; i++)
        {
            await AttemptAsync(client, email, "wrong");
        }

        await using var scope = Api.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email.Value == email);

        user.LockoutEndUtc.Should().NotBeNull();
        user.LockoutEndUtc!.Value.Should().BeAfter(DateTime.UtcNow);
    }

    /// <summary>
    /// The point of the whole mechanism: once locked, the *right* password stops working too.
    /// Without this, an attacker who guesses correctly on attempt six still gets in.
    /// </summary>
    [Fact]
    public async Task ALockedAccount_RefusesEvenTheCorrectPassword()
    {
        var email = await ProvisionUserAsync("refuse");
        using var client = Api.CreateClient();

        for (var i = 0; i < MaxAttempts; i++)
        {
            await AttemptAsync(client, email, "wrong");
        }

        var response = await AttemptAsync(client, email, TestPassword);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A lockout must be indistinguishable from a wrong password. "This account is locked"
    /// confirms the address exists and tells the caller exactly when to come back.
    /// </summary>
    [Fact]
    public async Task ALockout_IsNotDistinguishableFromAWrongPassword()
    {
        var email = await ProvisionUserAsync("opaque");
        using var client = Api.CreateClient();

        var beforeLock = await AttemptAsync(client, email, "wrong");
        var beforeBody = await beforeLock.Content.ReadAsStringAsync();

        for (var i = 0; i < MaxAttempts; i++)
        {
            await AttemptAsync(client, email, "wrong");
        }

        var afterLock = await AttemptAsync(client, email, TestPassword);
        var afterBody = await afterLock.Content.ReadAsStringAsync();

        afterLock.StatusCode.Should().Be(beforeLock.StatusCode);

        // traceId is per-request and carries no information about the account, so it is the
        // one field allowed to differ between the two responses.
        WithoutTraceId(afterBody).Should().Be(WithoutTraceId(beforeBody));
    }

    private static string WithoutTraceId(string body) =>
        Regex.Replace(body, "\"traceId\":\"[^\"]*\"", "\"traceId\":\"*\"");

    /// <summary>
    /// Locking one account must not lock the person out of the system, nor hint that the
    /// locked address was a real one.
    /// </summary>
    [Fact]
    public async Task LockingOneAccount_DoesNotAffectAnother()
    {
        var locked = await ProvisionUserAsync("victim");
        var bystander = await ProvisionUserAsync("bystand");
        using var client = Api.CreateClient();

        for (var i = 0; i < MaxAttempts; i++)
        {
            await AttemptAsync(client, locked, "wrong");
        }

        var response = await AttemptAsync(client, bystander, TestPassword);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
