using System.Globalization;
using System.Net;
using System.Text;
using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Application.Common.Html;
using AssignmentSystem.Domain.AcademicYears;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.ClassCourses;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Courses;
using AssignmentSystem.Domain.Enrollments;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssignmentSystem.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent database seeder. Builds a realistic, fully-populated sample school so an
/// evaluator sees a living system immediately instead of a few empty accounts.
///
/// Volume, all of it driven by <see cref="DemoCurriculum"/>:
///   • 7 grades (6–12) × 2 sections (A/B)                    = 14 classes
///   • 8 subjects, each a separate course per grade it is taught in:
///       - Bangla and English run 6–12                        (14 courses)
///       - General Mathematics and General Science, 6–8        (6 courses)
///       - Higher Mathematics, Physics, Chemistry, Biology, 9–12 (16 courses)
///     Codes carry the grade — BAN601, ENG601, BAN1101         = 36 courses
///   • every class studies its full subject list              = 72 course offerings
///   • 7 teachers, but only 2 offerings per section are mapped = 28 teaching mappings
///     (the other 44 are left blank on purpose, so the admin's teacher-mapping screen has
///      real work waiting for it instead of a school that is already fully wired)
///   • 5 students per class + 7 teachers + 1 admin            = 78 users
///   • the demo teacher's 8 offerings × 3 assignments          = 24 assignments,
///     one published per offering and two left as drafts       (8 published, 16 drafts)
///   • every assignment carries a real attachment, and every published one is submitted
///     to — with an attachment — and graded by all 5 students of its class = 40 submissions
///
/// Attachments are genuine files written through <c>IFileStorage</c>, not rows pointing at
/// nothing: a PDF worksheet, a plain-text instruction sheet, a PNG figure, and a PDF answer
/// sheet per submission. Viewing and downloading an attachment is a feature of the app, and it
/// cannot be demonstrated on a fresh checkout by metadata alone — see <see cref="DemoDocuments"/>.
///
/// Skips entirely once the admin account already exists.
///
/// Deliberately queues no notifications. They are a consequence of a teacher publishing or
/// a student submitting, and manufacturing a backlog of them would mean a fresh checkout
/// tries to email seventy fictional addresses the moment it starts. Publish an assignment from
/// the UI to see the outbox fill.
/// </summary>
public sealed class DbSeeder
{
    public const string AdminEmail = "admin@assignment.test";
    public const string TeacherEmail = "teacher@assignment.test";
    public const string StudentEmail = "student@assignment.test";

    // Demo passwords — documented in README. These are for local/demo only.
    public const string DefaultPassword = "Password123!";

    /// <summary>
    /// The teaching staff. Order matters: <see cref="DemoCurriculum.TeachingPlan"/> names
    /// teachers by their index here, and index <see cref="DemoCurriculum.DemoTeacherIndex"/> is
    /// the documented demo login — the mathematics and physics master, whose offerings are the
    /// ones that arrive with coursework already authored.
    /// </summary>
    private static readonly (string Email, string Name)[] TeacherSeats =
    [
        (TeacherEmail, "John Teacher"),
        ("teacher2@assignment.test", "Sarah Rahman"),
        ("teacher3@assignment.test", "Kamal Hossain"),
        ("teacher4@assignment.test", "Nusrat Jahan"),
        ("teacher5@assignment.test", "Farhan Ahmed"),
        ("teacher6@assignment.test", "Tahmina Akter"),
        ("teacher7@assignment.test", "Imran Chowdhury"),
    ];

    /// <summary>
    /// The month a session starts (July), which is what decides whether "now" belongs to the
    /// year it names or the one before. Only the seed assumes this — a real school enters its
    /// own dates, and nothing in the domain reads it.
    /// </summary>
    private const int SessionStartMonth = 7;

    /// <summary>
    /// One session running July of <paramref name="startYear"/> to June of the next, named
    /// for the pair ("2026-2027").
    /// </summary>
    private static AcademicYear MakeAcademicYear(int startYear, bool isCurrent) =>
        AcademicYear.Create(
            $"{startYear}-{startYear + 1}",
            new DateOnly(startYear, SessionStartMonth, 1),
            new DateOnly(startYear + 1, SessionStartMonth - 1, 30),
            isCurrent);

    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IFileStorage _fileStorage;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(
        AppDbContext context,
        IPasswordHasher passwordHasher,
        IFileStorage fileStorage,
        ILogger<DbSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _fileStorage = fileStorage;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SelfHealSubmissionCountsAsync(ct);

        if (await _context.Users.AnyAsync(u => u.Email.Value == AdminEmail, ct))
        {
            _logger.LogInformation("Seed already applied — skipping.");
            return;
        }

        _logger.LogInformation("Seeding demo data...");

        var clock = new SeederClock();
        var now = DateTime.UtcNow;
        var passwordHash = _passwordHasher.Hash(DefaultPassword);

        // ── Academic years (2): the running session and the one before it ─────────
        // Two rather than one so the year filter on the roster has something to filter and
        // the "current" badge means something on sight. Derived from the clock rather than
        // hardcoded, so a checkout in a later year still seeds a session that reads as now.
        var sessionStartYear = now.Month >= SessionStartMonth ? now.Year : now.Year - 1;
        var previousAcademicYear = MakeAcademicYear(sessionStartYear - 1, isCurrent: false);
        var currentAcademicYear = MakeAcademicYear(sessionStartYear, isCurrent: true);
        _context.AcademicYears.AddRange(previousAcademicYear, currentAcademicYear);

        // ── Classes (14): grades 6..12 × sections A/B ─────────────────────────────
        // Stored flat in a grade-major map so offerings and student placement can look
        // them up by (grade, section) without hunting through an array.
        var classByGradeSection = new Dictionary<(int Level, string Section), Class>();
        foreach (var level in DemoCurriculum.Levels)
        {
            foreach (var section in DemoCurriculum.Sections)
            {
                classByGradeSection[(level, section)] = Class.Create(level, section);
            }
        }

        var classes = classByGradeSection.Values.ToArray();
        _context.Classes.AddRange(classes);

        // ── Courses (36): one per (subject, grade) ────────────────────────────────
        // A subject is not one course shared by every grade that studies it — grade 6 Bangla
        // and grade 11 Bangla are different syllabuses taught to different rooms, and one row
        // for both would mean one teacher mapping and one assignment list for both. The grade
        // is encoded in the code, so BAN601 and BAN1101 are readable without a lookup.
        var courseBySubjectLevel = new Dictionary<(string Subject, int Level), Course>();
        foreach (var subject in DemoCurriculum.Subjects)
        {
            foreach (var level in subject.Levels)
            {
                courseBySubjectLevel[(subject.Name, level)] =
                    Course.Create(subject.Name, DemoCurriculum.CodeFor(subject, level));
            }
        }

        var courses = courseBySubjectLevel.Values.ToArray();
        _context.Courses.AddRange(courses);

        // ── Teachers (7, including the demo login) ────────────────────────────────
        // Mirrors the production rule in CreateUserHandler: "INS-{sequence}", a single
        // global sequence across all teachers.
        var teacherSequence = 0;
        string NextTeacherId() => $"INS-{++teacherSequence:D3}";

        var teachers = TeacherSeats
            .Select(t => ApplicationUser.Create(t.Email, t.Name, passwordHash, Role.Teacher, teacherId: NextTeacherId()))
            .ToArray();
        _context.Users.AddRange(teachers);
        var demoTeacher = teachers[DemoCurriculum.DemoTeacherIndex];

        // ── Admin ──────────────────────────────────────────────────────────────────
        var admin = ApplicationUser.Create(AdminEmail, "System Admin", passwordHash, Role.Admin);
        _context.Users.Add(admin);

        // ── Students (5 per class+section) ─────────────────────────────────────────
        // Mirrors the production rule in CreateUserHandler: "{grade}-{section}-{sequence}",
        // sequence numbers restarting at 1 per grade+section.
        var studentSequence = new Dictionary<string, int>(StringComparer.Ordinal);
        string NextStudentId(Class classRoom)
        {
            var prefix = $"{classRoom.Level}-{classRoom.Section}";
            var sequence = studentSequence.GetValueOrDefault(prefix, 0) + 1;
            studentSequence[prefix] = sequence;
            return $"{prefix}-{sequence:D3}";
        }

        // Bangladeshi sample names cycled through so each section gets plausible variety.
        var firstNames = new[]
        {
            "Arif", "Priya", "Tanvir", "Nadia", "Omar", "Lamia", "Zubair", "Rakib",
            "Sadia", "Mahin", "Farzana", "Hasib", "Ayesha", "Kamrul", "Sumaiya",
            "Naimur", "Tasnim", "Rifat", "Jannat", "Sabbir", "Mitu", "Shuvo",
            "Nusrat", "Galib", "Antora", "Rashed", "Sajid", "Liza", "Mehedi", "Rupa",
        };
        var lastNames = new[]
        {
            "Hasan", "Sultana", "Alam", "Islam", "Faruk", "Akter", "Rahman", "Hossain",
            "Chowdhury", "Khan", "Siddika", "Uddin", "Ahmed", "Jahan", "Akash",
        };

        // One seat is the demo login, placed in the class the demo teacher teaches two of —
        // see DemoCurriculum.DemoStudentLevel.
        var students = new List<ApplicationUser>();
        var studentsByClass = new Dictionary<(int Level, string Section), List<ApplicationUser>>();
        var studentPlacements = new List<(ApplicationUser Student, Class Class)>();
        var nameCursor = 0;

        foreach (var level in DemoCurriculum.Levels)
        {
            foreach (var section in DemoCurriculum.Sections)
            {
                var klass = classByGradeSection[(level, section)];
                var roster = new List<ApplicationUser>();
                studentsByClass[(level, section)] = roster;

                for (var seat = 0; seat < DemoCurriculum.StudentsPerSection; seat++)
                {
                    // The demo class's first seat is the documented demo login.
                    var isDemo = level == DemoCurriculum.DemoStudentLevel
                        && section == DemoCurriculum.DemoStudentSection
                        && seat == 0;

                    var first = firstNames[nameCursor % firstNames.Length];
                    var last = lastNames[(nameCursor * 7 + 3) % lastNames.Length];
                    nameCursor++;

                    var email = isDemo ? StudentEmail : $"student{students.Count + 1}@assignment.test";
                    var name = isDemo ? "Jane Student" : $"{first} {last}";

                    var student = ApplicationUser.Create(email, name, passwordHash, Role.Student, NextStudentId(klass));
                    students.Add(student);
                    roster.Add(student);
                    studentPlacements.Add((student, klass));
                }
            }
        }

        _context.Users.AddRange(students);

        await _context.SaveChangesAsync(ct); // persist to resolve generated IDs

        // ── Enrollments: one class each, matching the placements above ─────────────
        // Materialized rather than added straight from the projection so the summary below
        // reports what was actually written instead of a number that happens to match.
        // All in the current session: the seeded assignments and submissions belong to it,
        // and back-dating some students into the previous year would make the demo rosters
        // disagree with the coursework hanging off them.
        var enrollments = studentPlacements
            .Select(p => StudentEnrollment.Create(p.Student.Id, p.Class.Id, currentAcademicYear.Id, now))
            .ToList();
        _context.StudentEnrollments.AddRange(enrollments);

        // ── Course offerings (72): every class studies its full subject list ───────
        var offeringByClassSubject = new Dictionary<(int Level, string Section, string Subject), ClassCourse>();
        foreach (var level in DemoCurriculum.Levels)
        {
            foreach (var section in DemoCurriculum.Sections)
            {
                var klass = classByGradeSection[(level, section)];
                foreach (var subject in DemoCurriculum.SubjectsFor(level))
                {
                    offeringByClassSubject[(level, section, subject.Name)] =
                        ClassCourse.Create(klass.Id, courseBySubjectLevel[(subject.Name, level)].Id);
                }
            }
        }

        _context.ClassCourses.AddRange(offeringByClassSubject.Values);
        await _context.SaveChangesAsync(ct);

        // ── Teaching mappings (28): two offerings per section ──────────────────────
        // The plan is explicit rather than a round-robin: each teacher keeps to their own
        // subjects, and the demo teacher has to hold the demo student's class or that login
        // would show an empty dashboard. Everything not named here is deliberately unmapped.
        var teacherAssignments = new List<TeacherAssignment>();
        var mappingByClassSubject = new Dictionary<(int Level, string Section, string Subject), TeacherAssignment>();
        foreach (var (level, section, subject, teacherIndex) in DemoCurriculum.TeachingPlan)
        {
            var offering = offeringByClassSubject[(level, section, subject)];
            var mapping = TeacherAssignment.Create(teachers[teacherIndex].Id, offering.Id);

            teacherAssignments.Add(mapping);
            mappingByClassSubject[(level, section, subject)] = mapping;
        }

        _context.TeacherAssignments.AddRange(teacherAssignments);
        await _context.SaveChangesAsync(ct);

        // ── Assignments: three per offering the demo teacher holds ─────────────────
        // Authored by the teacher mapped to the offering, which is the rule
        // CreateAssignmentHandler enforces. The first of each three is published and the other
        // two stay drafts, so the teacher login shows both halves of the authoring workflow and
        // the student login sees only what it should.
        var seeded = new List<SeededAssignment>();
        foreach (var (level, section, subject, teacherIndex) in DemoCurriculum.TeachingPlan)
        {
            if (teacherIndex != DemoCurriculum.DemoTeacherIndex)
            {
                continue;
            }

            var mapping = mappingByClassSubject[(level, section, subject)];
            var course = courseBySubjectLevel[(subject, level)];
            var briefs = DemoCurriculum.BriefsFor(subject, level);

            for (var i = 0; i < briefs.Count; i++)
            {
                var brief = briefs[i];
                var published = i == 0;

                var assignment = Assignment.Create(
                    teacherId: mapping.TeacherId,
                    classCourseId: mapping.ClassCourseId,
                    title: brief.Title,
                    description: DescriptionHtml(brief, KindFor(i)),
                    deadlineUtc: now.AddDays(brief.DueInDays),
                    maxMarks: brief.MaxMarks,
                    allowResubmission: true,
                    clock: clock);

                if (published)
                {
                    assignment.Publish();
                }

                seeded.Add(new SeededAssignment(assignment, brief, i, level, section, subject, course.Code, published));
            }
        }

        _context.Assignments.AddRange(seeded.Select(s => s.Assignment));
        await _context.SaveChangesAsync(ct); // assignment ids, which the attachments need

        // ── Assignment attachments: a real file on every one ──────────────────────
        // The published assignment of each three carries both a worksheet and a figure, so an
        // evaluator can see a PDF and an image previewed without hunting for one; the drafts
        // carry a worksheet and a plain-text instruction sheet respectively. All three types the
        // in-app viewer can render are therefore present from the first login.
        var assignmentFiles = new List<AssignmentFile>();
        foreach (var item in seeded)
        {
            var subtitle = DocumentSubtitle(item);
            var blocks = BriefBlocks(item.Brief);
            var kind = KindFor(item.IndexInSet);
            var uploadedAt = now.AddHours(-6);

            var material = kind == AttachmentKind.Instructions
                ? DemoDocuments.PlainText($"{item.Brief.Title} - instructions", item.Brief.Title, subtitle, blocks)
                : DemoDocuments.Pdf($"{item.Brief.Title} - worksheet", item.Brief.Title, subtitle, blocks);

            assignmentFiles.Add(await AttachToAssignmentAsync(item.Assignment, demoTeacher.Id, material, uploadedAt, ct));

            if (kind == AttachmentKind.WorksheetAndFigure)
            {
                assignmentFiles.Add(await AttachToAssignmentAsync(
                    item.Assignment,
                    demoTeacher.Id,
                    DemoDocuments.Figure($"{item.Brief.Title} - figure", item.Brief.FigureVariant),
                    uploadedAt,
                    ct));
            }
        }

        _context.AssignmentFiles.AddRange(assignmentFiles);

        // ── Submissions: every published assignment, submitted and graded ─────────
        // The whole class hands in, each with an attachment, and every one of them is marked —
        // so both the student's "my marks" view and the teacher's marked-work view are populated
        // on the first login rather than after an evaluator has done the work by hand.
        //
        // Timestamps are anchored to "now minus a few days", which is always inside the
        // deadline (the nearest published one is six days out), so each row reads as Submitted
        // before it is graded rather than tripping the late rule.
        var seededSubmissions = new List<SeededSubmission>();
        foreach (var item in seeded.Where(s => s.Published))
        {
            var roster = studentsByClass[(item.Level, item.Section)];

            for (var seat = 0; seat < roster.Count; seat++)
            {
                var student = roster[seat];
                var note = DemoCurriculum.AnswerNotes[seat % DemoCurriculum.AnswerNotes.Length];
                var (fraction, feedback) = DemoCurriculum.GradeBands[seat % DemoCurriculum.GradeBands.Length];

                var submittedAt = now.AddHours(-96 + (seat * 9));
                var reviewedAt = now.AddHours(-40 + (seat * 5));

                var submission = Submission.Create(
                    item.Assignment.Id,
                    student.Id,
                    hasFile: true,
                    item.Assignment,
                    new FixedClock(submittedAt),
                    finalize: true);

                item.Assignment.IncrementSubmissionCount();

                submission.Grade(
                    Math.Round(item.Brief.MaxMarks * fraction, 2),
                    feedback,
                    item.Assignment.TeacherId,
                    item.Assignment,
                    new FixedClock(reviewedAt));

                seededSubmissions.Add(new SeededSubmission(submission, student, item, seat, note, submittedAt));
            }
        }

        _context.Submissions.AddRange(seededSubmissions.Select(s => s.Submission));
        await _context.SaveChangesAsync(ct); // submission ids, which the attachments need

        // ── Submission attachments: the work each student handed in ───────────────
        var submissionFiles = new List<SubmissionFile>();
        foreach (var item in seededSubmissions)
        {
            var document = AnswerDocument(item);
            var saved = await StoreAsync(document, ct);

            var file = SubmissionFile.Create(
                item.Submission.Id,
                item.Student.Id,
                saved.StoredFileName,
                document.FileName,
                document.ContentType,
                saved.SizeBytes,
                saved.RelativePath,
                item.SubmittedAtUtc);

            item.Submission.AttachFile(file);
            submissionFiles.Add(file);
        }

        _context.SubmissionFiles.AddRange(submissionFiles);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seed complete: {Classes} classes, {Courses} courses, {Offerings} offerings, {Teachers} teachers, " +
            "{Students} students, {Enrollments} enrollments, {TeacherAssignments} teaching mappings " +
            "({UnmappedOfferings} offerings left unmapped on purpose), " +
            "{Assignments} assignments ({PublishedAssignments} published) with {AssignmentFiles} attachments, " +
            "{Submissions} submissions (all graded) with {SubmissionFiles} attachments. " +
            "Demo logins — admin={Admin}, teacher={Teacher}, student={Student}",
            classes.Length, courses.Length, offeringByClassSubject.Count, teachers.Length,
            students.Count, enrollments.Count, teacherAssignments.Count,
            offeringByClassSubject.Count - teacherAssignments.Count,
            seeded.Count, seeded.Count(s => s.Published), assignmentFiles.Count,
            seededSubmissions.Count, submissionFiles.Count,
            AdminEmail, TeacherEmail, StudentEmail);
    }

    // ── Coursework rendering ──────────────────────────────────────────────────

    /// <summary>An assignment and the brief it was built from, kept together for the later passes.</summary>
    private sealed record SeededAssignment(
        Assignment Assignment,
        DemoBrief Brief,
        int IndexInSet,
        int Level,
        string Section,
        string Subject,
        string CourseCode,
        bool Published);

    /// <summary>A submission and everything its answer sheet needs to name.</summary>
    private sealed record SeededSubmission(
        Submission Submission,
        ApplicationUser Student,
        SeededAssignment Assignment,
        int Seat,
        string Note,
        DateTime SubmittedAtUtc);

    /// <summary>What a brief hands out, which is both what gets written and what the brief says.</summary>
    private enum AttachmentKind
    {
        /// <summary>A PDF worksheet and a PNG figure — the published one of each set.</summary>
        WorksheetAndFigure,
        Worksheet,
        Instructions,
    }

    /// <summary>
    /// One decision for both the description and the files, so a brief cannot promise an
    /// attachment it does not carry. The published assignment of each three hands out a worksheet
    /// and a figure and the last a plain-text instruction sheet, which between them put all three
    /// previewable types in front of an evaluator on the first login.
    /// </summary>
    private static AttachmentKind KindFor(int indexInSet) => indexInSet switch
    {
        0 => AttachmentKind.WorksheetAndFigure,
        2 => AttachmentKind.Instructions,
        _ => AttachmentKind.Worksheet,
    };

    /// <summary>
    /// The brief as document content. The assignment's description and its attachment are both
    /// rendered from this, which is what stops the worksheet from promising a different set of
    /// questions than the brief on screen.
    /// </summary>
    private static List<DemoBlock> BriefBlocks(DemoBrief brief)
    {
        var blocks = new List<DemoBlock>
        {
            DemoBlock.Paragraph(brief.Focus),
            DemoBlock.Heading("What to do"),
        };

        blocks.AddRange(brief.Tasks.Select(DemoBlock.Numbered));
        blocks.Add(DemoBlock.Heading("How to hand it in"));
        blocks.AddRange(DemoCurriculum.SubmissionRules.Select(DemoBlock.Bullet));

        return blocks;
    }

    /// <summary>
    /// The brief as the sanitized HTML the description column holds. Run through
    /// <see cref="HtmlContent.Sanitize"/> rather than trusted as written, so a seeded row is
    /// byte-for-byte the shape the rich-text editor's own write path would have produced.
    /// </summary>
    private static string DescriptionHtml(DemoBrief brief, AttachmentKind kind)
    {
        var html = new StringBuilder();

        Paragraph(html, brief.Focus);

        html.Append("<p><strong>What to do</strong></p><ol>");
        foreach (var task in brief.Tasks)
        {
            html.Append("<li>").Append(WebUtility.HtmlEncode(task)).Append("</li>");
        }

        html.Append("</ol>");

        html.Append("<p><strong>How to hand it in</strong></p><ul>");
        foreach (var rule in DemoCurriculum.SubmissionRules)
        {
            html.Append("<li>").Append(WebUtility.HtmlEncode(rule)).Append("</li>");
        }

        html.Append("</ul>");

        Paragraph(html, kind switch
        {
            AttachmentKind.Instructions =>
                "The attached instruction sheet repeats these tasks section by section, in a form you can print and keep beside your work.",
            AttachmentKind.WorksheetAndFigure =>
                "Open the attached worksheet before you begin — it carries the full question set, section by section, with the marks for each part printed beside it. The figure attached with it is the grid to plot your answers on.",
            _ =>
                "Open the attached worksheet before you begin — it carries the full question set, section by section, with the marks for each part printed beside it.",
        });

        return HtmlContent.Sanitize(html.ToString());

        static void Paragraph(StringBuilder target, string text) =>
            target.Append("<p>").Append(WebUtility.HtmlEncode(text)).Append("</p>");
    }

    /// <summary>The line under a document's title, naming the course and the room it was set for.</summary>
    private static string DocumentSubtitle(SeededAssignment item) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{item.Subject} ({item.CourseCode}) — Class {item.Level}, Section {item.Section}");

    /// <summary>
    /// What each student handed in. Written from the brief's own task list so the answer sheet
    /// answers the questions the worksheet asked, and varied by seat so a marking queue does not
    /// read as one student copied five times.
    /// </summary>
    private static DemoDocument AnswerDocument(SeededSubmission item)
    {
        var remarks = new[]
        {
            "Completed. Every step is written out, and the final answer is boxed.",
            "Completed. I checked the result by substituting it back into the original.",
            "Completed, with the diagram drawn to scale on the reverse of this page.",
            "Completed. I have named the rule used at each step, as the brief asked.",
        };

        var blocks = new List<DemoBlock>
        {
            DemoBlock.Paragraph(item.Note),
            DemoBlock.Heading("Task by task"),
        };

        for (var i = 0; i < item.Assignment.Brief.Tasks.Length; i++)
        {
            blocks.Add(DemoBlock.Numbered(remarks[(item.Seat + i) % remarks.Length]));
        }

        blocks.Add(DemoBlock.Heading("Declaration"));
        blocks.Add(DemoBlock.Paragraph(string.Create(
            CultureInfo.InvariantCulture,
            $"This is my own work. {item.Student.FullName}, roll {item.Student.StudentId}, " +
            $"Class {item.Assignment.Level} Section {item.Assignment.Section}.")));

        return DemoDocuments.Pdf(
            $"{item.Student.StudentId} - answer sheet",
            $"{item.Assignment.Brief.Title} — answers",
            DocumentSubtitle(item.Assignment),
            blocks);
    }

    // ── Storage ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes the bytes through the same storage abstraction an upload uses, so seeded
    /// attachments live under the configured root in the same layout and are read back by the
    /// same download path.
    /// </summary>
    private async Task<SavedFile> StoreAsync(DemoDocument document, CancellationToken ct)
    {
        using var content = new MemoryStream(document.Content, writable: false);
        return await _fileStorage.SaveAsync(content, Path.GetExtension(document.FileName), ct);
    }

    private async Task<AssignmentFile> AttachToAssignmentAsync(
        Assignment assignment,
        Guid uploadedById,
        DemoDocument document,
        DateTime uploadedAtUtc,
        CancellationToken ct)
    {
        var saved = await StoreAsync(document, ct);

        var file = AssignmentFile.Create(
            assignment.Id,
            uploadedById,
            saved.StoredFileName,
            document.FileName,
            document.ContentType,
            saved.SizeBytes,
            saved.RelativePath,
            uploadedAtUtc);

        assignment.AttachFile(file);
        return file;
    }

    private async Task SelfHealSubmissionCountsAsync(CancellationToken ct)
    {
        // Self-healing: align SubmissionCount cache values for any existing assignments
        var assignmentsToFix = await _context.Assignments
            .Where(a => a.SubmissionCount == 0)
            .ToListAsync(ct);

        bool needsSave = false;
        foreach (var assignmentToFix in assignmentsToFix)
        {
            var actualCount = await _context.Submissions.CountAsync(s => s.AssignmentId == assignmentToFix.Id, ct);
            if (actualCount > 0)
            {
                for (int i = 0; i < actualCount; i++)
                {
                    assignmentToFix.IncrementSubmissionCount();
                }

                needsSave = true;
            }
        }

        if (needsSave)
        {
            await _context.SaveChangesAsync(ct);
            _logger.LogInformation("Database self-healed: updated SubmissionCount on existing assignments.");
        }
    }

    /// <summary>Fixed clock so seeded assignment deadlines are validated against the real time.</summary>
    private sealed class SeederClock : Domain.Common.IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>A clock frozen at a specific instant, for backdating seeded submission/grading timestamps.</summary>
    private sealed class FixedClock : Domain.Common.IClock
    {
        private readonly DateTime _instant;
        public FixedClock(DateTime instant) => _instant = instant;
        public DateTime UtcNow => _instant;
    }
}
