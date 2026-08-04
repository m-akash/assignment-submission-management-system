using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Resolves the authenticated principal into an application identity. Handlers use
/// this instead of trusting client-supplied user ids. Implemented in Api from
/// HttpContext claims.
///
/// Class membership is deliberately absent: a student's classes are enrollment rows that
/// an admin can change at any time, so handlers read them through
/// <see cref="IClassRosterRepository"/> instead of from a claim that would go stale until
/// the token expired.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    string? FullName { get; }
    Role? Role { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(Role role);
}
