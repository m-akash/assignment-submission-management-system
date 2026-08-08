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
/// Every message follows the same shape: an eyebrow pill naming what kind of mail this is, a
/// title naming the thing it is about, a short lede, a detail table of the structured facts,
/// then whatever body content is specific to the event (a description, feedback, files…), and
/// finally a call to action. The goal is that every fact the recipient would otherwise have to
/// open the app to find — who, what, when, how much, attached to what — is already in the mail.
///
/// <b>Escaping is this class's responsibility.</b> It is the boundary that knows which entity
/// fields are user-authored (assignment titles and descriptions, teacher feedback, names, IDs,
/// submitted text) and runs every such value through <see cref="WebUtility.HtmlEncode"/> before
/// it reaches <see cref="EmailTemplates"/>, which trusts its inputs. A blanket escape over whole
/// assembled strings would escape the template's own tags, so escaping is per-value at the point
/// of use.
///
/// Subjects stay plain text on purpose: they are not rendered as HTML anywhere, and the test
/// suite asserts on them as substrings.
/// </summary>
internal static class NotificationMessages
{
    /// <summary>Submitted text answers are shown in full up to this length; longer ones are
    /// clipped with a pointer to the app, so one very long answer cannot blow up the mail.</summary>
    private const int ContentPreviewLimit = 600;

    /// <summary>Teacher feedback is shown in full up to this length, mirroring the content
    /// preview limit — feedback can run to the domain's 2000-character cap.</summary>
    private const int FeedbackPreviewLimit = 800;

    public static (string Subject, string Body) AssignmentPublished(
        Assignment assignment, ClassCourse offering, string recipientName, string appBaseUrl)
    {
        var subject = $"New assignment: {assignment.Title} ({offering.Course.Name})";
        var teacherName = assignment.Teacher?.FullName;

        var rows = new List<(string, string)>
        {
            ("Course", $"{Esc(offering.Course.Name)} ({Esc(offering.Course.Code)})"),
            ("Class", Esc(offering.Class.Name)),
        };
        if (!string.IsNullOrWhiteSpace(teacherName))
        {
            rows.Add(("Posted by", Esc(teacherName)));
        }
        rows.Add(("Deadline", Esc(FormatDeadline(assignment.DeadlineUtc))));
        rows.Add(("Max marks", Esc(FormatMarks(assignment.MaxMarks))));
        rows.Add(("Resubmission", assignment.AllowResubmission
            ? EmailTemplates.Badge("Allowed until the deadline", Tone.Success)
            : EmailTemplates.Badge("Not allowed — single submission", Tone.Warning)));

        var content = new StringBuilder()
            .Append(EmailTemplates.Eyebrow("New assignment"))
            .Append(EmailTemplates.Title(Esc(assignment.Title)))
            .Append(Paragraph($"Hello {Esc(recipientName)},"))
            .Append(Paragraph("A new assignment has been published for your class. Here are the details:"))
            .Append(DetailTable([.. rows]))
            .Append(Heading("Description"))
            .Append(Paragraph(Esc(assignment.Description)));

        if (assignment.Files.Count > 0)
        {
            content.Append(Heading($"Reference materials ({assignment.Files.Count})"))
                   .Append(Note("Attached by your teacher — open the assignment in the app to download."))
                   .Append(FileList(assignment.Files.Select(f => (f.OriginalFileName, f.FileSizeBytes))));
        }

        content.Append(Cta(appBaseUrl, "/assignments", "Open your assignments"));

        return (subject, Wrap($"New assignment \"{assignment.Title}\" — due {FormatDeadline(assignment.DeadlineUtc)}.", content.ToString()));
    }

    public static (string Subject, string Body) SubmissionReceived(
        Assignment assignment,
        ClassCourse offering,
        string teacherName,
        string studentName,
        Submission submission,
        string appBaseUrl,
        string? studentIdNumber = null)
    {
        var subject = $"Submission received: {assignment.Title} — {studentName}";
        var isLate = submission.Status == SubmissionStatus.Late;

        var rows = new List<(string, string)>
        {
            ("Course", $"{Esc(offering.Course.Name)} ({Esc(offering.Course.Code)})"),
            ("Class", Esc(offering.Class.Name)),
            ("Student", Esc(studentName)),
        };
        if (!string.IsNullOrWhiteSpace(studentIdNumber))
        {
            rows.Add(("Student ID", Esc(studentIdNumber)));
        }
        rows.Add(("Status", StatusBadge(submission.Status)));
        rows.Add(("Submitted", Esc(FormatDeadline(submission.SubmittedAtUtc ?? submission.CreatedAtUtc))));
        rows.Add(("Deadline was", Esc(FormatDeadline(assignment.DeadlineUtc))));

        var content = new StringBuilder()
            .Append(EmailTemplates.Eyebrow("Submission received", isLate ? Tone.Warning : Tone.Brand))
            .Append(EmailTemplates.Title(Esc(assignment.Title)))
            .Append(Paragraph($"Hello {Esc(teacherName)},"))
            .Append(Paragraph($"{Esc(studentName)} has submitted work for your assignment:"));

        if (isLate)
        {
            content.Append(InfoBox(
                "This submission arrived after the deadline and has been marked <strong>Late</strong>.",
                Tone.Warning));
        }

        content.Append(DetailTable([.. rows]));

        if (!string.IsNullOrWhiteSpace(submission.Content))
        {
            content.Append(Heading("Written answer"))
                   .Append(Quote(EscTruncated(submission.Content, ContentPreviewLimit)));
        }

        if (submission.Files.Count > 0)
        {
            content.Append(Heading($"Attachments ({submission.Files.Count})"))
                   .Append(FileList(submission.Files.Select(f => (f.OriginalFileName, f.FileSizeBytes))));
        }

        content.Append(Cta(appBaseUrl, "/submissions", "Review submissions"));

        var preheader = isLate
            ? $"{studentName} submitted {assignment.Title} — after the deadline."
            : $"{studentName} submitted {assignment.Title}.";
        return (subject, Wrap(preheader, content.ToString()));
    }

    public static (string Subject, string Body) SubmissionGraded(
        Assignment assignment, ClassCourse offering, string studentName, Submission submission, string appBaseUrl)
    {
        var subject = $"Your submission was graded: {assignment.Title}";
        var wasLate = submission.SubmittedAtUtc is { } submittedAt && submittedAt > assignment.DeadlineUtc;

        var rows = new List<(string, string)>
        {
            ("Course", $"{Esc(offering.Course.Name)} ({Esc(offering.Course.Code)})"),
            ("Class", Esc(offering.Class.Name)),
            ("Score", Esc(FormatScore(submission))),
        };
        if (submission.Marks is { } marks)
        {
            rows.Add(("Result", EmailTemplates.GradeBadge(marks, submission.MarksOutOf ?? 0m)));
        }
        if (submission.ReviewedBy is { } reviewer)
        {
            rows.Add(("Graded by", Esc(reviewer.FullName)));
        }
        if (submission.ReviewedAtUtc is { } reviewedAt)
        {
            rows.Add(("Graded on", Esc(FormatDeadline(reviewedAt))));
        }
        if (submission.SubmittedAtUtc is { } submittedOn)
        {
            rows.Add(("You submitted", Esc(FormatDeadline(submittedOn))));
        }
        if (wasLate)
        {
            rows.Add(("Timing", EmailTemplates.Badge("Late submission", Tone.Warning)));
        }

        var content = new StringBuilder()
            .Append(EmailTemplates.Eyebrow("Graded", Tone.Success))
            .Append(EmailTemplates.Title(Esc(assignment.Title)))
            .Append(Paragraph($"Hello {Esc(studentName)},"))
            .Append(Paragraph("Your submission has been reviewed and graded."));

        if (submission.Marks is { } m && submission.MarksOutOf is { } outOf)
        {
            content.Append(EmailTemplates.ScoreBar(m, outOf));
        }

        content.Append(DetailTable([.. rows]));

        if (!string.IsNullOrWhiteSpace(submission.Feedback))
        {
            content.Append(Heading("Feedback from your teacher"))
                   .Append(Quote(EscTruncated(submission.Feedback, FeedbackPreviewLimit)));
        }
        else
        {
            content.Append(Note("Your teacher did not leave written feedback for this submission."));
        }

        if (submission.Files.Count > 0)
        {
            content.Append(Heading($"Your submitted files ({submission.Files.Count})"))
                   .Append(FileList(submission.Files.Select(f => (f.OriginalFileName, f.FileSizeBytes))));
        }

        content.Append(Cta(appBaseUrl, "/assignments", "View your marks and feedback"));

        return (subject, Wrap($"Your submission for {assignment.Title} has been graded: {FormatScore(submission)}.", content.ToString()));
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
            ("Role", Esc(Capitalize(role))),
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
            .Append(EmailTemplates.Eyebrow("Account ready"))
            .Append(EmailTemplates.Title($"Your {Esc(role)} account is ready"))
            .Append(Paragraph($"Hello {Esc(user.FullName)},"))
            .Append(Paragraph(
                "An account has been created for you on the Assignment &amp; Submission Management " +
                "System. Use the button below to choose your own password and finish setting it up."))
            .Append(DetailTable([.. rows]));

        if (string.IsNullOrWhiteSpace(appBaseUrl))
        {
            content.Append(InfoBox(
                "No application URL is configured on the server, so the setup link could not be " +
                "built. Ask your administrator to set <code>Email__AppBaseUrl</code> and re-send this.",
                Tone.Danger));
        }
        else
        {
            var url = $"{appBaseUrl.TrimEnd('/')}/set-password?token={setupToken}";
            // The button renders the URL as visible fallback text beneath it, so the link
            // survives even when a mail client strips the styled anchor.
            content.Append(EmailTemplates.Button(url, "Choose your password"));
        }

        content.Append(InfoBox(
            $"This link works once and expires on <strong>{Esc(FormatDeadline(expiresAtUtc))}</strong>. " +
            "If it has expired by the time you open it, ask your administrator to send a new one.",
            Tone.Warning));

        content.Append(Note(
            "If you were not expecting this email, no action is needed — no changes are made to " +
            "this account until the link above is used."));

        return (subject, Wrap($"Your {role} account is ready. Choose a password to activate it.", content.ToString()));
    }

    public static (string Subject, string Body) TeacherAssignedToCourse(
        ClassCourse offering,
        string teacherName,
        string appBaseUrl,
        string? teacherIdNumber = null,
        int? enrolledStudentCount = null)
    {
        var subject = $"You have been assigned to teach {offering.Course.Name} ({offering.Class.Name})";

        var rows = new List<(string, string)>
        {
            ("Course", $"{Esc(offering.Course.Name)} ({Esc(offering.Course.Code)})"),
            ("Class", Esc(offering.Class.Name)),
        };
        if (!string.IsNullOrWhiteSpace(teacherIdNumber))
        {
            rows.Add(("Teacher ID", Esc(teacherIdNumber)));
        }
        if (enrolledStudentCount is { } count)
        {
            rows.Add(("Enrolled students", Esc(count == 1 ? "1 student" : $"{count} students")));
        }

        var content = new StringBuilder()
            .Append(EmailTemplates.Eyebrow("New course assignment"))
            .Append(EmailTemplates.Title($"{Esc(offering.Course.Name)} — {Esc(offering.Class.Name)}"))
            .Append(Paragraph($"Hello {Esc(teacherName)},"))
            .Append(Paragraph("You have been assigned to teach a course. You can now create and publish assignments for this class, and grade the work that comes in."))
            .Append(DetailTable([.. rows]))
            .Append(Cta(appBaseUrl, "/assignments", "Open your assignments"))
            .ToString();

        return (subject, Wrap($"You have been assigned to teach {offering.Course.Name} for {offering.Class.Name}.", content));
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
        Class @class,
        IReadOnlyList<Course> courses,
        string studentName,
        string? studentIdNumber,
        string appBaseUrl,
        int? classmateCount = null)
    {
        var subject = $"You have been enrolled in {@class.Name}";

        var rows = new List<(string, string)>
        {
            ("Class", Esc(@class.Name)),
        };
        if (!string.IsNullOrWhiteSpace(studentIdNumber))
        {
            rows.Add(("Student ID", Esc(studentIdNumber)));
        }
        if (classmateCount is { } count and > 0)
        {
            rows.Add(("Classmates", Esc(count == 1 ? "1 other student" : $"{count} other students")));
        }
        rows.Add(("Courses", Esc(courses.Count == 1 ? "1 course" : $"{courses.Count} courses")));

        var content = new StringBuilder()
            .Append(EmailTemplates.Eyebrow("Class enrollment"))
            .Append(EmailTemplates.Title($"Welcome to {Esc(@class.Name)}"))
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

    private static string Quote(string html) => EmailTemplates.Quote(html);

    private static string InfoBox(string html, Tone tone) => EmailTemplates.InfoBox(html, tone);

    private static string FileList(IEnumerable<(string Name, long SizeBytes)> files) => EmailTemplates.FileList(files);

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
            .Append("font-size:14px;line-height:1.9;color:#1e1e2a;\">");
        foreach (var course in courses)
        {
            sb.Append("<li><strong>").Append(Esc(course.Name))
              .Append("</strong> <span style=\"color:#6b6b80;\">(").Append(Esc(course.Code))
              .Append(")</span></li>");
        }
        return sb.Append("</ul>").ToString();
    }

    /// <summary>A tone-coloured pill for a submission's lifecycle status.</summary>
    private static string StatusBadge(SubmissionStatus status) => status switch
    {
        SubmissionStatus.Late => EmailTemplates.Badge("Late", Tone.Warning),
        SubmissionStatus.Graded => EmailTemplates.Badge("Graded", Tone.Success),
        SubmissionStatus.Submitted => EmailTemplates.Badge("Submitted", Tone.Info),
        _ => EmailTemplates.Badge(status.ToString(), Tone.Neutral),
    };

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

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    /// <summary>
    /// Deadline shown in 12-hour AM/PM. Everything is stored in UTC and the offset is omitted
    /// to keep the wording clean; recipients see a UTC instant without a "UTC" suffix.
    /// </summary>
    private static string FormatDeadline(DateTime utc) =>
        utc.ToString("dd MMM yyyy, hh:mm tt", CultureInfo.InvariantCulture);

    private static string FormatMarks(decimal marks) =>
        marks.ToString("0.##", CultureInfo.InvariantCulture);

    private static string FormatScore(Submission submission) =>
        submission.Marks is { } marks
            ? $"{FormatMarks(marks)} / {FormatMarks(submission.MarksOutOf ?? 0m)}"
            : "not recorded";

    /// <summary>HTML-escape a user-supplied value for safe insertion into the template.</summary>
    private static string Esc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>
    /// Escapes, then clips to <paramref name="limit"/> characters with an ellipsis and a note
    /// pointing at the app — so one very long answer or feedback comment cannot make the mail
    /// itself unreasonably large. Escaping happens first so clipping can never land mid-entity
    /// (e.g. splitting "&amp;" in two).
    /// </summary>
    private static string EscTruncated(string? value, int limit)
    {
        var escaped = Esc(value);
        if (escaped.Length <= limit)
        {
            return escaped;
        }

        return escaped[..limit] + "&hellip; <em>(continued in the app)</em>";
    }
}
