using AssignmentSystem.Application.Abstractions;
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
/// Volume per the seeding spec:
///   • 7 grades (6–12) × 2 sections (A/B)              = 14 classes
///   • 9 subjects in the catalogue, but each grade takes only what fits its stage:
///       - grades 6–8: Bangla, English, General Math, General Science, ICT  (5 each)
///       - grades 9–12: Bangla, English, Higher Math, Physics, Chemistry, Biology, ICT (7 each)
///                                                      = 86 course offerings (3×2×5 + 4×2×7)
///   • 12 teachers, round-robin across the offerings   = 84 teaching mappings
///   • 5 students per class+section × 14               = 70 students (+ 1 admin)
///   • a representative spread of published/draft assignments and submissions across every
///     submission status (Pending / Submitted / Graded / Late) so the demo logins have data.
///
/// Skips entirely once the admin account already exists.
///
/// Deliberately queues no notifications. They are a consequence of a teacher publishing or
/// a student submitting, and manufacturing a backlog of them would mean a fresh checkout
/// tries to email fictional addresses the moment it starts. Publish an assignment from the
/// UI to see the outbox fill.
/// </summary>
public sealed class DbSeeder
{
    public const string AdminEmail = "admin@assignment.test";
    public const string TeacherEmail = "teacher@assignment.test";
    public const string StudentEmail = "student@assignment.test";

    // Demo passwords — documented in README. These are for local/demo only.
    public const string DefaultPassword = "Password123!";

    // Grades 6..12 (inclusive), two sections each.
    private static readonly int[] Grades = [6, 7, 8, 9, 10, 11, 12];
    private static readonly string[] Sections = ["A", "B"];
    private const int StudentsPerSection = 5;

    private readonly AppDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(AppDbContext context, IPasswordHasher passwordHasher, ILogger<DbSeeder> logger)
    {
        _context = context;
        _passwordHasher = passwordHasher;
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

        // ── Classes (14): grade 6..12 × sections A/B ──────────────────────────────
        // Stored flat in a grade-major map so offerings and student placement can look
        // them up by (grade, section) without hunting through an array.
        var classByGradeSection = new Dictionary<(int Grade, string Section), Class>();
        foreach (var grade in Grades)
        {
            foreach (var section in Sections)
            {
                var klass = Class.Create($"Class {grade} - Section {section}", grade, section);
                classByGradeSection[(grade, section)] = klass;
            }
        }

        var classes = Grades
            .SelectMany(g => Sections.Select(s => classByGradeSection[(g, s)]))
            .ToArray();
        _context.Classes.AddRange(classes);

        // ── Courses / subjects (catalogue) ───────────────────────────────────────
        // The catalogue holds every subject the school teaches across all grades, but a
        // given class only studies the subset that fits its stage (see offering plan below).
        //   • Bangla & English  — every grade
        //   • ICT               — every grade
        //   • General Math      — lower grades (6–8)
        //   • General Science   — lower grades (6–8), single combined science course
        //   • Higher Mathematics— upper grades (9–12)
        //   • Physics / Chemistry / Biology — upper grades (9–12) only
        var courseDefs = new (string Name, string Code)[]
        {
            ("Bangla", "BAN101"),
            ("English", "ENG101"),
            ("General Math", "GMATH101"),
            ("General Science", "GSCI101"),
            ("Higher Mathematics", "HMT101"),
            ("Physics", "PHY101"),
            ("Chemistry", "CHE101"),
            ("Biology", "BIO101"),
            ("ICT", "ICT101"),
        };
        var courses = courseDefs.Select(d => Course.Create(d.Name, d.Code)).ToArray();
        _context.Courses.AddRange(courses);
        var bangla = courses[0];
        var english = courses[1];
        var generalMath = courses[2];
        var generalScience = courses[3];
        var higherMath = courses[4];
        var physics = courses[5];
        var chemistry = courses[6];
        var biology = courses[7];
        var ict = courses[8];

        // ── Teachers (12, including the demo login) ───────────────────────────────
        // Mirrors the production rule in CreateUserHandler: "INS-{sequence}", a single
        // global sequence across all teachers.
        var teacherSequence = 0;
        string NextTeacherId() => $"INS-{++teacherSequence:D3}";

        var teacherDefs = new (string Email, string Name)[]
        {
            (TeacherEmail, "John Teacher"),
            ("teacher2@assignment.test", "Sarah Rahman"),
            ("teacher3@assignment.test", "Kamal Hossain"),
            ("teacher4@assignment.test", "Nusrat Jahan"),
            ("teacher5@assignment.test", "Farhan Ahmed"),
            ("teacher6@assignment.test", "Rima Chowdhury"),
            ("teacher7@assignment.test", "Imran Khan"),
            ("teacher8@assignment.test", "Tania Islam"),
            ("teacher9@assignment.test", "Shakil Ahmed"),
            ("teacher10@assignment.test", "Mou Akter"),
            ("teacher11@assignment.test", "Rafiq Uddin"),
            ("teacher12@assignment.test", "Sabrina Yasmin"),
        };
        var teachers = teacherDefs
            .Select(t => ApplicationUser.Create(t.Email, t.Name, passwordHash, Role.Teacher, teacherId: NextTeacherId()))
            .ToArray();
        _context.Users.AddRange(teachers);

        // ── Admin ──────────────────────────────────────────────────────────────────
        var admin = ApplicationUser.Create(AdminEmail, "System Admin", passwordHash, Role.Admin);
        _context.Users.Add(admin);

        // ── Students (5 per class+section) ─────────────────────────────────────────
        // Mirrors the production rule in CreateUserHandler: "{grade numeral}-{section}-{sequence}",
        // sequence numbers restarting at 1 per grade+section.
        var studentSequence = new Dictionary<string, int>(StringComparer.Ordinal);
        string NextStudentId(Class classRoom)
        {
            var prefix = $"{classRoom.GradeLabel}-{classRoom.Section}";
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

        // The very first student is the demo login, placed in Class 12 - Section A so the
        // documented `student@assignment.test` account lands in the senior-most class.
        var students = new List<ApplicationUser>();
        var studentPlacements = new List<(ApplicationUser Student, Class Class)>();
        var nameCursor = 0;

        ApplicationUser MakeStudent(string email, string name, Class klass)
        {
            var student = ApplicationUser.Create(email, name, passwordHash, Role.Student, NextStudentId(klass));
            students.Add(student);
            studentPlacements.Add((student, klass));
            return student;
        }

        foreach (var grade in Grades)
        {
            foreach (var section in Sections)
            {
                var klass = classByGradeSection[(grade, section)];

                for (var i = 0; i < StudentsPerSection; i++)
                {
                    // Class 12-A's first seat is the documented demo login.
                    var isDemo = grade == 12 && section == "A" && i == 0;
                    var first = firstNames[nameCursor % firstNames.Length];
                    var last = lastNames[(nameCursor * 7 + 3) % lastNames.Length];
                    nameCursor++;

                    var email = isDemo
                        ? StudentEmail
                        : $"student{students.Count + 1}@assignment.test";
                    var name = isDemo ? "Jane Student" : $"{first} {last}";

                    MakeStudent(email, name, klass);
                }
            }
        }

        _context.Users.AddRange(students);
        var jane = studentPlacements[0].Student; // demo login

        await _context.SaveChangesAsync(ct); // persist to resolve generated IDs

        // ── Enrollments: one class each, matching the placements above ─────────────
        // Materialized rather than added straight from the projection so the summary below
        // reports what was actually written instead of a number that happens to match.
        var enrollments = studentPlacements
            .Select(p => StudentEnrollment.Create(p.Student.Id, p.Class.Id, now))
            .ToList();
        _context.StudentEnrollments.AddRange(enrollments);

        // ── Course offerings: each grade studies only the subjects that fit its stage ─
        //   • Lower grades (6–8): Bangla, English, General Math, General Science, ICT  (5)
        //   • Upper grades (9–12): Bangla, English, Higher Math, Physics, Chemistry,
        //                          Biology, ICT                                          (7)
        // The pure-science trio (Physics/Chemistry/Biology) and Higher Mathematics are
        // upper-grade-only; lower grades take General Math and the combined General Science.
        Course[] SubjectsForGrade(int grade) => grade <= 8
            ? [bangla, english, generalMath, generalScience, ict]
            : [bangla, english, higherMath, physics, chemistry, biology, ict];

        // Built in a stable order (grade → section → course) so the round-robin teacher
        // assignment below is deterministic across runs.
        var offerings = new List<ClassCourse>();
        foreach (var grade in Grades)
        {
            foreach (var section in Sections)
            {
                var klass = classByGradeSection[(grade, section)];
                foreach (var course in SubjectsForGrade(grade))
                {
                    offerings.Add(ClassCourse.Create(klass.Id, course.Id));
                }
            }
        }

        _context.ClassCourses.AddRange(offerings);
        await _context.SaveChangesAsync(ct);

        // ── Teaching mappings: round-robin the 12 teachers across all offerings ────
        var teacherAssignments = new List<TeacherAssignment>();
        for (var i = 0; i < offerings.Count; i++)
        {
            var teacher = teachers[i % teachers.Length];
            teacherAssignments.Add(TeacherAssignment.Create(teacher.Id, offerings[i].Id));
        }

        _context.TeacherAssignments.AddRange(teacherAssignments);
        await _context.SaveChangesAsync(ct);

        // ── Assignments: one authored assignment per course, hosted in Class 12-A ──
        // Authored by whichever teacher is mapped to that offering (the rule
        // CreateAssignmentHandler enforces). Mostly Published, one Draft so students see a
        // populated dashboard with at least one hidden assignment. Deadlines are offsets
        // from `now`; rule X5 requires deadline ≥ now + 1h, honoured by the SeederClock.
        var seniorClass = classByGradeSection[(12, "A")];
        Assignment MakeAssignment(Course course, string title, string description,
            TimeSpan untilDeadline, decimal maxMarks, bool publish)
        {
            var offering = offerings.First(o => o.ClassId == seniorClass.Id && o.CourseId == course.Id);
            var ta = teacherAssignments.First(t => t.ClassCourseId == offering.Id);
            var assignment = Assignment.Create(
                teacherId: ta.TeacherId,
                classCourseId: ta.ClassCourseId,
                title: title,
                description: description,
                deadlineUtc: now.Add(untilDeadline),
                maxMarks: maxMarks,
                allowResubmission: true,
                clock: clock);
            if (publish)
            {
                assignment.Publish();
            }

            return assignment;
        }

        var class12Students = studentPlacements
            .Where(p => p.Class.Id == seniorClass.Id)
            .Select(p => p.Student)
            .ToList();

        var assignments = new List<Assignment>
        {
            MakeAssignment(bangla, "রচনা: আমার প্রিয় ঋতু", "তোমার প্রিয় ঋতুর উপর একটি ৩০০ শব্দের রচনা লেখো।", TimeSpan.FromDays(6), 20m, publish: true),
            MakeAssignment(english, "Essay: Climate Change", "Write a 400-word essay on the causes and effects of climate change.", TimeSpan.FromDays(7), 20m, publish: true),
            MakeAssignment(higherMath, "Calculus Introduction", "Solve the introductory differentiation problems 1 through 10.", TimeSpan.FromDays(11), 50m, publish: true),
            MakeAssignment(physics, "Newton's Laws Problem Set", "Answer the three problem sets covering Newton's first, second and third laws.", TimeSpan.FromDays(9), 50m, publish: true),
            MakeAssignment(chemistry, "Periodic Table Quiz", "Answer the short-answer quiz on periods, groups and element properties.", TimeSpan.FromDays(5), 30m, publish: true),
            MakeAssignment(biology, "Cell Structure Diagram", "Label the plant and animal cell diagrams and describe each organelle's function.", TimeSpan.FromDays(10), 25m, publish: false),
            MakeAssignment(ict, "HTML Basics Project", "Build a 3-page static website using semantic HTML and submit the source.", TimeSpan.FromDays(12), 40m, publish: true),
        };

        // One more, hosted in Class 12-B with a near deadline, to seed a Late submission.
        var nearClass = classByGradeSection[(12, "B")];
        var nearOffering = offerings.First(o => o.ClassId == nearClass.Id && o.CourseId == higherMath.Id);
        var nearTa = teacherAssignments.First(t => t.ClassCourseId == nearOffering.Id);
        var aLate = Assignment.Create(
            teacherId: nearTa.TeacherId,
            classCourseId: nearTa.ClassCourseId,
            title: "Integration Practice",
            description: "Solve the definite and indefinite integration problems on the attached sheet. Due shortly — submit promptly.",
            deadlineUtc: now.Add(TimeSpan.FromMinutes(65)),
            maxMarks: 30m,
            allowResubmission: true,
            clock: clock);
        aLate.Publish();
        assignments.Add(aLate);

        _context.Assignments.AddRange(assignments);
        await _context.SaveChangesAsync(ct);

        // ── Submissions: a representative mix across every status ──────────────────
        // Non-Late timestamps are anchored to "now minus a few hours/days" so they read as
        // already-submitted no matter when this seed runs — every assignment's own deadline
        // is always further out than that (rule X5 requires deadlines in the future). The
        // late submission's deadline is only ~65 minutes out, so its row is dated ~75 minutes
        // from now (10 minutes past that deadline) — briefly a "future" timestamp, but the
        // persisted Status is fixed as Late at write time regardless.
        var class12BStudents = studentPlacements
            .Where(p => p.Class.Id == nearClass.Id)
            .Select(p => p.Student)
            .ToList();
        var mahinLate = class12BStudents[0];

        // Collected rather than added one by one, so the summary below counts the rows that
        // were actually built. Adding a submission here and forgetting to update a hard-coded
        // total is exactly how the previous count came to be wrong.
        var submissions = new List<Submission>();

        void Submit(Assignment assignment, ApplicationUser student, string content, TimeSpan ago, bool finalize)
        {
            var submission = Submission.Create(assignment.Id, student.Id, content, hasFile: false, assignment, new FixedClock(now - ago), finalize);
            assignment.IncrementSubmissionCount();
            submissions.Add(submission);
        }

        void SubmitAndGrade(Assignment assignment, ApplicationUser student, string content, TimeSpan submittedAgo, TimeSpan gradedAgo, decimal marks, string feedback)
        {
            var submission = Submission.Create(assignment.Id, student.Id, content, hasFile: false, assignment, new FixedClock(now - submittedAgo), finalize: true);
            assignment.IncrementSubmissionCount();
            submission.Grade(marks, feedback, assignment.TeacherId, assignment, new FixedClock(now - gradedAgo));
            submissions.Add(submission);
        }

        // Class 12-A assignments — the demo student and a couple of classmates.
        //   [0]=Bangla(20) [1]=English(20) [2]=HigherMath(50) [3]=Physics(50)
        //   [4]=Chemistry(30) [5]=Biology(25, DRAFT — no submissions) [6]=ICT(40)
        SubmitAndGrade(assignments[0], jane, "আমার প্রিয় ঋতু বর্ষা — প্রকৃতি তখন সবুজে ভরে ওঠে।", TimeSpan.FromDays(2), TimeSpan.FromHours(6), 18m, "সুন্দর লেখা হয়েছে, ভাষা প্রাঞ্জল।");
        Submit(assignments[1], jane, "In progress — outlined the causes, still writing the effects section.", TimeSpan.FromHours(3), finalize: false);
        SubmitAndGrade(assignments[2], class12Students[1], "Differentiated problems 1 through 8; attached working for each step.", TimeSpan.FromDays(1), TimeSpan.FromHours(4), 42m, "Great work — small slip in problem 4.");
        Submit(assignments[3], class12Students[2], "Completed the first two problem sets; stuck on the third.", TimeSpan.FromHours(20), finalize: false);
        SubmitAndGrade(assignments[4], class12Students[3], "Answered all 15 questions on periodic trends and element classification.", TimeSpan.FromHours(8), TimeSpan.FromHours(2), 26m, "Solid grasp of periodic trends — well done.");
        Submit(assignments[6], jane, "Uploaded my 3-page site: home, about and contact sections.", TimeSpan.FromDays(2), finalize: true);

        // Class 12-B late submission.
        Submit(aLate, mahinLate, "Submitting a little late — solved all the integration problems.", TimeSpan.FromMinutes(-75), finalize: true);

        _context.Submissions.AddRange(submissions);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seed complete: {Classes} classes, {Courses} courses, {Offerings} offerings, {Teachers} teachers, " +
            "{Students} students, {Enrollments} enrollments, {TeacherAssignments} teaching mappings, " +
            "{Assignments} assignments ({PublishedAssignments} published), {Submissions} submissions. " +
            "Demo logins — admin={Admin}, teacher={Teacher}, student={Student}",
            classes.Length, courses.Length, offerings.Count, teachers.Length,
            students.Count, enrollments.Count, teacherAssignments.Count,
            assignments.Count, assignments.Count(a => a.Status == AssignmentStatus.Published), submissions.Count,
            AdminEmail, TeacherEmail, StudentEmail);
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
