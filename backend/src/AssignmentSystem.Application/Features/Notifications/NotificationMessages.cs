using System.Globalization;
using System.Text;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Courses;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Domain.Users;

namespace AssignmentSystem.Application.Features.Notifications;

/// <summary>
/// Composes the subject and body of each notification email. Kept as pure functions over
/// already-loaded entities: no repositories, no clock, no configuration beyond the base
/// URL that is passed in — so the wording is unit-testable on its own, and the queue
/// stays about persistence rather than prose.
///
/// Bodies are plain text on purpose. An HTML template would need escaping, a text
/// fallback, and inline-CSS gymnastics to survive real mail clients; plain text renders
/// identically everywhere and cannot carry an injection.
/// </summary>
internal static class NotificationMessages
{
    public static (string Subject, string Body) AssignmentPublished(
        Assignment assignment, ClassCourse offering, string recipientName, string appBaseUrl)
    {
        var subject = $"New assignment: {assignment.Title} ({offering.Course.Name})";

        var body = new StringBuilder()
            .Append(Greeting(recipientName))
            .Append("A new assignment has been published for your class.").Append("\n\n")
            .Append("  Assignment : ").Append(assignment.Title).Append('\n')
            .Append("  Course     : ").Append(offering.Course.Name).Append(" (").Append(offering.Course.Code).Append(")\n")
            .Append("  Class      : ").Append(offering.Class.Name).Append('\n')
            .Append("  Deadline   : ").Append(FormatDeadline(assignment.DeadlineUtc)).Append('\n')
            .Append("  Max marks  : ").Append(FormatMarks(assignment.MaxMarks)).Append("\n\n")
            .Append(assignment.Description).Append("\n\n")
            .Append(Link(appBaseUrl, "/assignments", "Open your assignments"))
            .Append(Signature())
            .ToString();

        return (subject, body);
    }

    public static (string Subject, string Body) SubmissionReceived(
        Assignment assignment, ClassCourse offering, string teacherName, string studentName, Submission submission, string appBaseUrl)
    {
        var subject = $"Submission received: {assignment.Title} — {studentName}";

        var body = new StringBuilder()
            .Append(Greeting(teacherName))
            .Append(studentName).Append(" has submitted work for your assignment.").Append("\n\n")
            .Append("  Assignment : ").Append(assignment.Title).Append('\n')
            .Append("  Course     : ").Append(offering.Course.Name).Append(" (").Append(offering.Course.Code).Append(")\n")
            .Append("  Class      : ").Append(offering.Class.Name).Append('\n')
            .Append("  Student    : ").Append(studentName).Append('\n')
            .Append("  Status     : ").Append(submission.Status).Append('\n')
            .Append("  Submitted  : ").Append(FormatDeadline(submission.SubmittedAtUtc ?? submission.CreatedAtUtc)).Append("\n\n")
            .Append(Link(appBaseUrl, "/submissions", "Review submissions"))
            .Append(Signature())
            .ToString();

        return (subject, body);
    }

    public static (string Subject, string Body) SubmissionGraded(
        Assignment assignment, ClassCourse offering, string studentName, Submission submission, string appBaseUrl)
    {
        var subject = $"Your submission was graded: {assignment.Title}";

        var body = new StringBuilder()
            .Append(Greeting(studentName))
            .Append("Your submission has been reviewed.").Append("\n\n")
            .Append("  Assignment : ").Append(assignment.Title).Append('\n')
            .Append("  Course     : ").Append(offering.Course.Name).Append(" (").Append(offering.Course.Code).Append(")\n")
            .Append("  Marks      : ").Append(FormatScore(submission)).Append('\n');

        if (!string.IsNullOrWhiteSpace(submission.Feedback))
        {
            body.Append("\nFeedback from your teacher:\n").Append(submission.Feedback).Append('\n');
        }

        body.Append('\n')
            .Append(Link(appBaseUrl, "/assignments", "View your marks and feedback"))
            .Append(Signature());

        return (subject, body.ToString());
    }

    /// <summary>
    /// The account-created mail. Carries a single-use link and deliberately no password:
    /// email is plaintext in transit through relays nobody here operates, is retained in
    /// mailboxes indefinitely, and is the one channel a password must never be committed to.
    /// The link proves control of the mailbox; the password itself is chosen by its owner and
    /// only ever crosses the HTTPS request that sets it.
    ///
    /// <paramref name="appBaseUrl"/> being unset is the one case that cannot degrade to a
    /// bodyless mention, so it is stated rather than dropped — an account-created mail with
    /// no way to act on it would leave the recipient stuck with no idea why.
    /// </summary>
    public static (string Subject, string Body) AccountCreated(
        ApplicationUser user, string setupToken, DateTime expiresAtUtc, string appBaseUrl)
    {
        var subject = $"Your {RoleWording(user.Role)} account is ready";

        var body = new StringBuilder()
            .Append(Greeting(user.FullName))
            .Append("An account has been created for you on the Assignment & Submission Management System.")
            .Append("\n\n")
            .Append("  Role  : ").Append(RoleWording(user.Role)).Append('\n')
            .Append("  Email : ").Append(user.EmailValue).Append('\n');

        // The school id is how staff will refer to them, so it belongs in the first mail.
        if (user.StudentId is { } studentId)
        {
            body.Append("  Student ID : ").Append(studentId).Append('\n');
        }
        else if (user.TeacherId is { } teacherId)
        {
            body.Append("  Teacher ID : ").Append(teacherId).Append('\n');
        }

        body.Append('\n')
            .Append("Choose your password to finish setting up. This link works once and expires on ")
            .Append(FormatDeadline(expiresAtUtc)).Append(":\n\n");

        if (string.IsNullOrWhiteSpace(appBaseUrl))
        {
            body.Append(
                "  (No application URL is configured on the server, so the link could not be\n" +
                "   built. Ask your administrator to set Email__AppBaseUrl and re-send this.)\n");
        }
        else
        {
            body.Append("  ").Append(appBaseUrl.TrimEnd('/'))
                .Append("/set-password?token=").Append(setupToken).Append('\n');
        }

        body.Append('\n')
            .Append("If the link has expired by the time you open it, ask your administrator to send a new one.")
            .Append('\n')
            .Append(Signature());

        return (subject, body.ToString());
    }

    public static (string Subject, string Body) TeacherAssignedToCourse(
        ClassCourse offering, string teacherName, string appBaseUrl)
    {
        var subject = $"You have been assigned to teach {offering.Course.Name} ({offering.Class.Name})";

        var body = new StringBuilder()
            .Append(Greeting(teacherName))
            .Append("You have been assigned to teach a course.").Append("\n\n")
            .Append("  Course : ").Append(offering.Course.Name).Append(" (").Append(offering.Course.Code).Append(")\n")
            .Append("  Class  : ").Append(offering.Class.Name).Append('\n')
            .Append("  Grade  : ").Append(offering.Class.GradeLabel)
            .Append(offering.Class.Section is { } section ? $", Section {section}" : string.Empty).Append("\n\n")
            .Append("You can now create and publish assignments for this class, and grade the work that comes in.").Append("\n\n")
            .Append(Link(appBaseUrl, "/assignments", "Open your assignments"))
            .Append(Signature())
            .ToString();

        return (subject, body);
    }

    /// <summary>
    /// Enrollment is a class membership, so this is one mail about the class — with the
    /// courses it studies listed inside it, since "which subjects am I taking?" is the
    /// question a student actually has on being enrolled.
    ///
    /// <paramref name="courses"/> may be empty: a class can exist before any course is
    /// added to it, and the student should still be told they are in it.
    /// </summary>
    public static (string Subject, string Body) StudentEnrolled(
        Class @class, IReadOnlyList<Course> courses, string studentName, string? studentIdNumber, string appBaseUrl)
    {
        var subject = $"You have been enrolled in {@class.Name}";

        var body = new StringBuilder()
            .Append(Greeting(studentName))
            .Append("You have been enrolled in a class.").Append("\n\n")
            .Append("  Class : ").Append(@class.Name).Append('\n')
            .Append("  Grade : ").Append(@class.GradeLabel)
            .Append(@class.Section is { } section ? $", Section {section}" : string.Empty).Append('\n');

        if (!string.IsNullOrWhiteSpace(studentIdNumber))
        {
            body.Append("  Student ID : ").Append(studentIdNumber).Append('\n');
        }

        body.Append('\n');

        if (courses.Count > 0)
        {
            body.Append("Courses you are now taking:\n");
            foreach (var course in courses)
            {
                body.Append("  • ").Append(course.Name).Append(" (").Append(course.Code).Append(")\n");
            }
        }
        else
        {
            // Stated rather than left as an empty gap — a class with no offerings yet is a
            // normal mid-setup state, and silence here reads like a broken email.
            body.Append("No courses have been added to this class yet. You will see them here once they are.\n");
        }

        body.Append('\n')
            .Append(Link(appBaseUrl, "/assignments", "Open your assignments"))
            .Append(Signature());

        return (subject, body.ToString());
    }

    /// <summary>
    /// The role as the recipient would describe themselves. <c>Role.ToString()</c> would do
    /// for two of the three, but "Your Admin account" reads as a system label rather than a
    /// sentence.
    /// </summary>
    private static string RoleWording(Role role) => role switch
    {
        Role.Admin => "administrator",
        Role.Teacher => "teacher",
        Role.Student => "student",
        _ => "user",
    };

    private static string Greeting(string name) =>
        string.IsNullOrWhiteSpace(name) ? "Hello,\n\n" : $"Hello {name},\n\n";

    private static string Signature() =>
        "\n—\nAssignment & Submission Management System\nThis is an automated message; please do not reply.\n";

    /// <summary>
    /// Renders the link only when a base URL is configured — a mail containing
    /// "http:///assignments" is worse than one containing no link at all.
    /// </summary>
    private static string Link(string appBaseUrl, string path, string label) =>
        string.IsNullOrWhiteSpace(appBaseUrl)
            ? string.Empty
            : $"{label}: {appBaseUrl.TrimEnd('/')}{path}\n";

    /// <summary>
    /// UTC, stated as UTC. Everything is stored in UTC and the recipient's timezone is
    /// not known, so labelling it beats quietly implying a local time it isn't.
    /// </summary>
    private static string FormatDeadline(DateTime utc) =>
        utc.ToString("dd MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string FormatMarks(decimal marks) =>
        marks.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatScore(Submission submission) =>
        submission.Marks is { } marks
            ? $"{FormatMarks(marks)} / {FormatMarks(submission.MarksOutOf ?? 0m)}"
            : "not recorded";
}
