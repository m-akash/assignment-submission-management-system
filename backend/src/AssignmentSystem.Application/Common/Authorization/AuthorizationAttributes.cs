using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Common.Authorization;

/// <summary>
/// Declares which roles may execute a command or query. Applied to the message type
/// itself rather than to the handler, so "who is allowed to do this?" is answerable from
/// the declaration — no reading a handler body to find out.
///
/// Enforced by <c>AuthorizationDecorator</c> at runtime and by
/// <see cref="AuthorizationPolicy.ValidateAllMessagesAreAnnotated"/> at startup: a message
/// carrying none of the attributes in this file will not let the application boot. That is
/// deliberate — the previous failure mode was a handler silently forgetting its role check,
/// and a forgotten annotation must be louder than a forgotten <c>if</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresRoleAttribute : Attribute
{
    public IReadOnlyList<Role> Roles { get; }

    public RequiresRoleAttribute(params Role[] roles)
    {
        if (roles is null || roles.Length == 0)
        {
            throw new ArgumentException("At least one role is required.", nameof(roles));
        }

        Roles = roles;
    }
}

/// <summary>
/// Any authenticated caller may execute this message; the handler narrows what they see.
/// Used where all three roles have a legitimate but differently-scoped view of the same
/// resource (an assignment list, say), so a role gate would be wrong but anonymous access
/// still is too.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequiresAuthenticationAttribute : Attribute;

/// <summary>
/// No identity required. Only for the messages that establish or recover one — logging in,
/// exchanging a refresh cookie, redeeming a password-setup link. Every use of this is a
/// public entry point and should be reviewed as such.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AllowAnonymousAttribute : Attribute;
