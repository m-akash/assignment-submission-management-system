namespace AssignmentSystem.Domain.Enums;

/// <summary>
/// Submission state machine.
/// Pending  → created/in-progress before the deadline.
/// Submitted→ student has submitted (before deadline).
/// Graded   → teacher has reviewed and assigned marks + feedback.
/// Late     → submitted after the deadline (rule X2).
/// </summary>
public enum SubmissionStatus
{
    Pending = 0,
    Submitted = 1,
    Graded = 2,
    Late = 3,
}
