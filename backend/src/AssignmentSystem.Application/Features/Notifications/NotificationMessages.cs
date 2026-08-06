using System.Globalization;
using System.Net;
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
/// Composes the subject and HTML body of each notification email. Kept as pure functions over
/// already-loaded entities: no repositories, no clock, no configuration beyond the base URL that
/// is passed in — so the wording is unit-testable on its own, and the queue stays about
/// persistence rather than prose.
///
/// <b>Escaping is this class's responsibility.</b> It is the boundary that knows which entity
/// fields are user-authored (assignment titles and descriptions, teacher feedback, names, IDs)
/// and runs every such value through <see cref="WebUtility.HtmlEncode"/> before it reaches
/// <see cref="EmailTemplates"/>, which trusts its inputs. A blanket escape over whole assembled
/// strings would escape the template's own tags, so escaping is per-value at the point of use.
///
/// Subjects stay plain text on purpose: they are not rendered as HTML anywhere, and the test
/// suite asserts on them as substrings.
/// </summary>
internal static class NotificationMessages
{
    public static (string Subject, string Body) AssignmentPublished(
        Assignment assignment, ClassCourse offering, string recipientName, string appBaseUrl)
    {
        var subject = $"New assignment: {assignment.Title} ({offering.Course.Name})";

        var content = new StringBuilder()
            .Append(Paragraph($"Hello {Esc(recipientName)},"))
            .Append(Paragraph("A new assignment has been published for your class. Here are the details:"))
            .Append(DetailTable(
                ("Assignment", Esc(assignment.Title)),
                ("Course", $"{Esc(offering.Course.Name)} ({Esc(offering.Course.Code)})"),
                ("Class", Esc(offering.Class.Name)),
                ("Deadline", Esc(FormatDeadline(assignment.DeadlineUtc))),
                ("Max marks", Esc(FormatMarks(assignment.MaxMarks)))))
            .Append(Heading("Description"))
            .Append(Paragraph(Esc(assignment.Description)))
            .Append(Cta(appBaseUrl, "/assignments", "Open your assignments"))
            .ToString();

        return (subject, Wrap("A new assignment has been published.", content));
    }

    public static (string Subject, string Body) SubmissionReceived(
        Assignment assignment, ClassCourse offering, string teacherName, string studentName, Submission submission, string appBaseUrl)
    {
        var subject = $"Submission received: {assignment.Title} — {studentName}";

        var content = new StringBuilder()
            .Append(Paragraph($"Hello {Esc(teacherName)},"))
            .Append(Paragraph($"{Esc(studentName)} has submitted work for your assignment:"))
            .Append(DetailTable(
                ("Assignment", Esc(assignment.Title)),
                ("Course", $"{Esc(offering.Course.Name)} ({Esc(offering.Course.Code)})"),
                ("Class", Esc(offering.Class.Name)),
                ("Student", Esc(studentName)),
                ("Status", Esc(submission.Status.ToString())),
                ("Submitted", Esc(FormatDeadline(submission.SubmittedAtUtc ?? submission.CreatedAtUtc)))))
            .Append(Cta(appBaseUrl, "/submissions", "Review submissions"))
            .ToString();

        return (subject, Wrap($"{studentName} submitted work for {assignment.Title}.", content));
    }

    public static (string Subject, string Body) SubmissionGraded(
        Assignment assignment, ClassCourse offering, string studentName, Submission submission, string appBaseUrl)
    {
        var subject = $"Your submission was graded: {assignment.Title}";

        var content = new StringBuilder()
            .Append(Paragraph($"Hello {Esc(studentName)},"))
            .Append(Paragraph("Your submission has been reviewed and graded."))
            .Append(DetailTable(
                ("Assignment", Esc(assignment.Title)),
                ("Course", $"{Esc(offering.Course.Name)} ({Esc(offering.Course.Code)})"),
                ("Class", Esc(offering.Class.Name)),
                ("Marks", Esc(FormatScore(submission)))));

        if (!string.IsNullOrWhiteSpace(submission.Feedback))
        {
            content.Append(Heading("Feedback from your teacher"))
                   .Append(Paragraph(Esc(submission.Feedback)));
        }

        content.Append(Cta(appBaseUrl, "/assignments", "View your marks and feedback"));

        return (subject, Wrap("Your submission has been graded.", content.ToString()));
    }

    /// <summary>
    /// The account-created mail. Carries a single-use link and deliberately no password:
    /// email is plaintext in transit through relays nobody here operates, is retained in
    /// mailboxes indefinitely, and is the one channel a password must never be committed to.
    /// The link proves control of the mailbox; the password itself is chosen by its owner and
    /// only ever crosses the HTTPS request that sets it.
    ///
    /// <paramref name="appBaseUrl"/> being unset is the one case that cannot degrade to a
    /// bodyless mention, so it is stated rather than dropped — an account-created mail with no
    /// way to act on it would leave the recipient stuck with no idea why.
    /// </summary>
    public static (string Subject, string Body) AccountCreated(
        ApplicationUser user, string setupToken, DateTime expiresAtUtc, string appBaseUrl)
    {
        var role = RoleWording(user.Role);
        var subject = $"Your {role} account is ready";

        var rows = new List<(string, string)>
        {
            ("Role", Esc(role)),
            ("Email", Esc(user.EmailValue)),
        };

        // The school id is how staff will refer to them, so it belongs in the first mail.
        if (user.StudentId is { } studentId)
        {
            rows.Add(("Student ID", Esc(studentId)));
        }
        else if (user.TeacherId is { } teacherId)
        {
            rows.Add(("Teacher ID", Esc(teacherId)));
        }

        var content = new StringBuilder()
            .Append(Paragraph($"Hello {Esc(user.FullName)},"))
            .Append(Paragraph(
                "An account has been created for you on the Assignment &amp; Submission Management " +
                "System. Use the button below to choose your own password and finish setting it up."))
            .Append(DetailTable([.. rows]));

        if (string.IsNullOrWhiteSpace(appBaseUrl))
        {
            content.Append(Note(
                "No application URL is configured on the server, so the setup link could not be " +
                "built. Ask your administrator to set <code>Email__AppBaseUrl</code> and re-send this."));
        }
        else
        {
            var url = $"{appBaseUrl.TrimEnd('/')}/set-password?token={setupToken}";
            // The button renders the URL as visible fallback text beneath it, so the link
            // survives even when a mail client strips the styled anchor.
            content.Append(EmailTemplates.Button(url, "Choose your password"));
        }

        content.Append(Note(
            $"This link works once and expires on {Esc(FormatDeadline(expiresAtUtc))}. " +
            "If it has expired by the time you open it, ask your administrator to send a new one."));

        return (subject, Wrap($"Your {role} account is ready. Choose a password to activate it.", content.ToString()));
    }

    public static (string Subject, string Body) TeacherAssignedToCourse(
        ClassCourse offering, string teacherName, string appBaseUrl)
    {
        var subject = $"You have been assigned to teach {offering.Course.Name} ({offering.Class.Name})";
        var gradeLine = Esc($"Grade {offering.Class.GradeLabel}") +
            (offering.Class.Section is { } section ? $", Section {Esc(section)}" : string.Empty);

        var content = new StringBuilder()
            .Append(Paragraph($"Hello {Esc(teacherName)},"))
            .Append(Paragraph("You have been assigned to teach a course. You can now create and publish assignments for this class, and grade the work that comes in."))
            .Append(DetailTable(
                ("Course", $"{Esc(offering.Course.Name)} ({Esc(offering.Course.Code)})"),
                ("Class", Esc(offering.Class.Name)),
                ("Level", gradeLine)))
            .Append(Cta(appBaseUrl, "/assignments", "Open your assignments"))
            .ToString();

        return (subject, Wrap("You have been assigned to teach a course.", content));
    }

    /// <summary>
    /// Enrollment is a class membership, so this is one mail about the class — with the
    /// courses it studies listed inside it, since "which subjects am I taking?" is the
    /// question a student actually has on being enrolled.
    ///
    /// <paramref name="courses"/> may be empty: a class can exist before any course is added
    /// to it, and the student should still be told they are in it.
    /// </summary>
    public static (string Subject, string Body) StudentEnrolled(
        Class @class, IReadOnlyList<Course> courses, string studentName, string? studentIdNumber, string appBaseUrl)
    {
        var subject = $"You have been enrolled in {@class.Name}";
        var gradeLine = Esc($"Grade {@class.GradeLabel}") +
            (@class.Section is { } section ? $", Section {Esc(section)}" : string.Empty);

        var rows = new List<(string, string)>
        {
            ("Class", Esc(@class.Name)),
            ("Level", gradeLine),
        };
        if (!string.IsNullOrWhiteSpace(studentIdNumber))
        {
            rows.Add(("Student ID", Esc(studentIdNumber)));
        }

        var content = new StringBuilder()
            .Append(Paragraph($"Hello {Esc(studentName)},"))
            .Append(Paragraph("You have been enrolled in a class."))
            .Append(DetailTable([.. rows]));

        if (courses.Count > 0)
        {
            content.Append(Heading("Courses you are now taking"))
                   .Append(CourseList(courses));
        }
        else
        {
            // Stated rather than left as an empty gap — a class with no offerings yet is a normal
            // mid-setup state, and silence here reads like a broken email.
            content.Append(Note(
                "No courses have been added to this class yet. You will see them here once they are."));
        }

        content.Append(Cta(appBaseUrl, "/assignments", "Open your assignments"));

        return (subject, Wrap($"You have been enrolled in {@class.Name}.", content.ToString()));
    }

    // ── Compose helpers (build fragments, then wrap in the shell) ───────────────────

    private static string Wrap(string preheader, string content) =>
        EmailTemplates.Shell(preheader, content);

    private static string Paragraph(string html) => EmailTemplates.Paragraph(html);

    private static string Heading(string text) => EmailTemplates.Heading(text);

    private static string Note(string html) => EmailTemplates.Note(html);

    private static string DetailTable(params (string Label, string Value)[] rows) =>
        EmailTemplates.DetailTable(rows);

    /// <summary>
    /// A CTA button, but only when a base URL is configured — a mail containing a dead
    /// "http:///assignments" button is worse than one with no button at all.
    /// </summary>
    private static string Cta(string appBaseUrl, string path, string label) =>
        string.IsNullOrWhiteSpace(appBaseUrl)
            ? string.Empty
            : EmailTemplates.Button($"{appBaseUrl.TrimEnd('/')}{path}", label);

    /// <summary>A bullet list of course name + code, as a clean HTML list.</summary>
    private static string CourseList(IReadOnlyList<Course> courses)
    {
        var sb = new StringBuilder()
            .Append("<ul style=\"margin:6px 0 14px;padding-left:20px;font-family:Arial,Helvetica,sans-serif;")
            .Append("font-size:14px;line-height:1.8;color:#1e1e2a;\">");
        foreach (var course in courses)
        {
            sb.Append("<li><strong>").Append(Esc(course.Name))
              .Append("</strong> <span style=\"color:#6b6b80;\">(").Append(Esc(course.Code))
              .Append(")</span></li>");
        }
        return sb.Append("</ul>").ToString();
    }

    // ── Wording / formatting (unchanged behaviour, kept from the plain-text version) ──

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

    /// <summary>
    /// UTC, stated as UTC. Everything is stored in UTC and the recipient's timezone is not
    /// known, so labelling it beats quietly implying a local time it isn't.
    /// </summary>
    private static string FormatDeadline(DateTime utc) =>
        utc.ToString("dd MMM yyyy, HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string FormatMarks(decimal marks) =>
        marks.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatScore(Submission submission) =>
        submission.Marks is { } marks
            ? $"{FormatMarks(marks)} / {FormatMarks(submission.MarksOutOf ?? 0m)}"
            : "not recorded";

    /// <summary>HTML-escape a user-supplied value for safe insertion into the template.</summary>
    private static string Esc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
