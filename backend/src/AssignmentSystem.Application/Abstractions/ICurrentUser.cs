using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Resolves the authenticated principal into an application identity. Handlers use
/// this instead of trusting client-supplied user ids. Implemented in Api from
/// HttpContext claims.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    string? FullName { get; }
    Role? Role { get; }
    Guid? ClassId { get; }
    Guid? GroupId { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(Role role);
}
