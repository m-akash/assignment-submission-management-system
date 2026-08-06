using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AssignmentSystem.Api.Controllers;
using FluentAssertions;
using Xunit;

namespace AssignmentSystem.Api.Tests;

/// <summary>
/// The per-IP cap on credential endpoints.
///
/// Runs against its own host rather than the shared one: the limit has to be set low enough
/// to trip deliberately, and every other test in the assembly signs in through the same
/// partition. Cheap to do — this factory reuses the shared database container.
/// </summary>
public sealed class RateLimitingTests : IntegrationTestBase
{
    private const int Limit = 3;

    public RateLimitingTests(ApiFactory api) : base(api) { }

    [Fact]
    public async Task Login_BeyondTheLimit_IsRefusedWith429()
    {
        using var factory = Api.CreateHostWithRateLimit(Limit);
        using var client = factory.CreateClient();

        // Wrong credentials on purpose: the limiter must not depend on the outcome of the
        // attempt, only on how many were made.
        var request = new LoginRequest("nobody@throttle.local", "wrong");

        for (var i = 0; i < Limit; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/v1/auth/login", request);
            allowed.StatusCode.Should().Be(
                HttpStatusCode.Unauthorized, "attempts within the limit reach the handler");
        }

        var refused = await client.PostAsJsonAsync("/api/v1/auth/login", request);

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// A 429 without Retry-After leaves a client guessing, and guessing clients retry in
    /// tight loops — which is the thing the limiter exists to prevent.
    /// </summary>
    [Fact]
    public async Task ARefusedRequest_TellsTheCallerWhenToRetry()
    {
        using var factory = Api.CreateHostWithRateLimit(Limit);
        using var client = factory.CreateClient();
        var request = new LoginRequest("nobody@throttle.local", "wrong");

        for (var i = 0; i <= Limit; i++)
        {
            await client.PostAsJsonAsync("/api/v1/auth/login", request);
        }

        var refused = await client.PostAsJsonAsync("/api/v1/auth/login", request);

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        refused.Headers.Should().ContainSingle(h => h.Key == "Retry-After");
        refused.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    /// <summary>
    /// The limit is scoped to the credential endpoints. Rate-limiting ordinary reads would
    /// punish a busy classroom for using the app.
    /// </summary>
    [Fact]
    public async Task OrdinaryEndpoints_AreNotRateLimited()
    {
        using var factory = Api.CreateHostWithRateLimit(Limit);
        using var client = factory.CreateClient();

        var responses = await Task.WhenAll(
            Enumerable.Range(0, Limit * 3).Select(_ => client.GetAsync("/api/v1/classes")));

        // Unauthorized because no token is attached — the point is that none are 429.
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Unauthorized);
    }
}
