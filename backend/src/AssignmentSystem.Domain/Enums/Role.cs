namespace AssignmentSystem.Domain.Enums;

/// <summary>
/// Application roles. Persisted as smallint; stored as a plain enum (not flags)
/// — each user has exactly one role.
/// </summary>
public enum Role
{
    Admin = 0,
    Teacher = 1,
    Student = 2,
}
