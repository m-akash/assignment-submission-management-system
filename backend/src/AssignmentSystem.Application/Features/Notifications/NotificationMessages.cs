using System.Globalization;
using System.Text;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Submissions;

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
