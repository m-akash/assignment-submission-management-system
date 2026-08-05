namespace AssignmentSystem.Domain.Enums;

/// <summary>
/// What a notification is about. Drives the subject/body the composer builds and lets
/// the admin outbox view be filtered by event rather than by reading subject lines.
/// </summary>
public enum NotificationType
{
    /// <summary>A teacher published an assignment — sent to every student enrolled in its class.</summary>
    AssignmentPublished = 0,

    /// <summary>A student submitted — sent to the teacher who owns the assignment.</summary>
    SubmissionReceived = 1,

    /// <summary>A teacher graded a submission — sent to the student who owns it.</summary>
    SubmissionGraded = 2,

    /// <summary>An admin gave a teacher a course offering to teach — sent to that teacher.</summary>
    TeacherAssignedToCourse = 3,

    /// <summary>
    /// A student was enrolled in a class — sent to that student. Enrollment is per class,
    /// not per course, so this is one mail listing the courses the class studies rather
    /// than one mail per course.
    /// </summary>
    StudentEnrolled = 4,

    /// <summary>
    /// An admin created the account — sent to its owner with a single-use link to choose
    /// their own password. Carries a link and never a password: the mail is the one part of
    /// this system that travels over infrastructure nobody here controls.
    /// </summary>
    AccountCreated = 5,
}
