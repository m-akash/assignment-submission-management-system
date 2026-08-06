using System.Collections.Concurrent;
using System.Reflection;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Handlers;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Shared.Common;

namespace AssignmentSystem.Application.Common.Authorization;

/// <summary>
/// Reads the authorization attributes off a message type and answers whether the current
/// caller may proceed. Resolution is cached per message type — the attribute lookup happens
/// once per type for the lifetime of the process, not once per request.
/// </summary>
internal static class AuthorizationPolicy
{
    private static readonly ConcurrentDictionary<Type, Policy> Cache = new();

    /// <summary>
    /// The gate itself. Returns the error to fail the pipeline with, or <c>null</c> to proceed.
    /// Unauthenticated is 401 and wrong-role is 403 — kept distinct because "sign in" and
    /// "you cannot do this" are different instructions to the caller.
    /// </summary>
    public static Error? Check(Type messageType, ICurrentUser currentUser)
    {
        var policy = Cache.GetOrAdd(messageType, Resolve);

        if (policy.AllowAnonymous)
        {
            return null;
        }

        if (!currentUser.IsAuthenticated || currentUser.Role is null)
        {
            return Error.Unauthorized("Auth.Required", "You must be signed in to perform this action.");
        }

        if (policy.Roles is null)
        {
            // RequiresAuthentication: any signed-in role, handler narrows the result set.
            return null;
        }

        return policy.Roles.Contains(currentUser.Role.Value)
            ? null
            : Error.Forbidden("Auth.Forbidden", "You do not have permission to perform this action.");
    }

    /// <summary>
    /// Startup guard: every command and query in the assembly must declare an authorization
    /// stance. Throws with the full list of offenders rather than the first one, so a
    /// developer adding several messages fixes them in one pass.
    /// </summary>
    public static void ValidateAllMessagesAreAnnotated(Assembly assembly)
    {
        var unannotated = assembly.GetTypes()
            .Where(IsMessage)
            .Where(t => !HasAuthorizationAttribute(t))
            .Select(t => t.FullName ?? t.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        if (unannotated.Count > 0)
        {
            throw new InvalidOperationException(
                $"{unannotated.Count} command/query type(s) declare no authorization stance. " +
                $"Add [RequiresRole(...)], [RequiresAuthentication] or [AllowAnonymous] to each of:{Environment.NewLine}" +
                string.Join(Environment.NewLine, unannotated.Select(n => "  - " + n)));
        }
    }

    private static bool IsMessage(Type type)
    {
        if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
        {
            return false;
        }

        return type.GetInterfaces().Any(i =>
            i == typeof(ICommand)
            || (i.IsGenericType
                && (i.GetGenericTypeDefinition() == typeof(ICommand<>)
                    || i.GetGenericTypeDefinition() == typeof(IQuery<>))));
    }

    private static bool HasAuthorizationAttribute(Type type) =>
        type.GetCustomAttribute<RequiresRoleAttribute>() is not null
        || type.GetCustomAttribute<RequiresAuthenticationAttribute>() is not null
        || type.GetCustomAttribute<AllowAnonymousAttribute>() is not null;

    private static Policy Resolve(Type messageType)
    {
        if (messageType.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
        {
            return new Policy(AllowAnonymous: true, Roles: null);
        }

        if (messageType.GetCustomAttribute<RequiresRoleAttribute>() is { } roleAttribute)
        {
            return new Policy(AllowAnonymous: false, Roles: [.. roleAttribute.Roles]);
        }

        if (messageType.GetCustomAttribute<RequiresAuthenticationAttribute>() is not null)
        {
            return new Policy(AllowAnonymous: false, Roles: null);
        }

        // Unreachable once the startup guard has run, but a message reaching the pipeline
        // without a stance must deny rather than default open.
        return new Policy(AllowAnonymous: false, Roles: []);
    }

    private sealed record Policy(bool AllowAnonymous, Role[]? Roles);
}
