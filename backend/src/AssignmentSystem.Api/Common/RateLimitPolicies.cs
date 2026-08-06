namespace AssignmentSystem.Api.Common;

/// <summary>
/// Named rate-limiting policies. A constant rather than a literal on each endpoint so a
/// typo is a compile error instead of an endpoint that silently isn't limited.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Applied to every endpoint that accepts or acts on a credential — signing in, trading a
    /// refresh cookie, and both halves of the password-setup link. Partitioned per client
    /// address.
    /// </summary>
    public const string Credentials = "credentials";
}
