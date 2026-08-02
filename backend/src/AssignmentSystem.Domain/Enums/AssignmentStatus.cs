namespace AssignmentSystem.Domain.Enums;

/// <summary>
/// Lifecycle of an assignment. Draft → Published is a one-way transition
/// (rule B6). Students cannot see or submit to Draft assignments (rule X3).
/// </summary>
public enum AssignmentStatus
{
    Draft = 0,
    Published = 1,
}
