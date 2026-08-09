using AssignmentSystem.Application.Abstractions;
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
/// Volume per the seeding spec:
///   • 4 grades (7–10) × 2 sections (A/B)              = 8 classes
///   • 8 subjects in the catalogue, but each grade takes only what fits its stage:
///       - grades 7–8: Bangla, English, General Math, General Science  (4 each)
///       - grades 9–10: Physics, Chemistry, Higher Mathematics, Biology, Bangla, English (6 each)
///                                                      = 40 course offerings (2×2×4 + 2×2×6)
///   • 5 teachers, round-robin across the offerings    = 35 teaching mappings
///     (5 offerings are left deliberately unmapped, so the admin's teacher-mapping screen has
///      real work waiting for it instead of a fully-wired school — see UnassignedOfferings.)
///   • 5 students per class+section × 8                = 40 students (+ 1 admin)
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

    // Grades 7..10 (inclusive), two sections each.
    private static readonly int[] Grades = [7, 8, 9, 10];
    private static readonly string[] Sections = ["A", "B"];
    private const int StudentsPerSection = 5;

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

    /// <summary>The grade the demo student login sits in — the senior-most, where the seeded
    /// assignments and submissions live.</summary>
    private const int SeniorGrade = 10;

    /// <summary>
    /// Offerings left without a teacher on purpose, identified by (grade, section, course code).
    /// The admin's "Teacher Mappings" screen is the feature being demonstrated, so the seed
    /// stops just short of finishing the job and leaves five real gaps to fill by hand.
    /// Chosen to avoid grade 10 section A entirely (every seeded assignment is hosted there)
    /// and grade 10 section B's Higher Mathematics (the late-submission assignment), because an
    /// assignment cannot exist without the teacher mapping it is authored under.
    /// </summary>
    private static readonly (int Grade, string Section, string CourseCode)[] UnassignedOfferings =
    [
        (7, "B", "GSCI101"),
        (8, "A", "GMATH101"),
        (8, "B", "ENG101"),
        (9, "B", "BIO101"),
        (10, "B", "CHE101"),
    ];

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

        // ── Academic years (2): the running session and the one before it ─────────
        // Two rather than one so the year filter on the roster has something to filter and
        // the "current" badge means something on sight. Derived from the clock rather than
        // hardcoded, so a checkout in a later year still seeds a session that reads as now.
        var sessionStartYear = now.Month >= SessionStartMonth ? now.Year : now.Year - 1;
        var previousAcademicYear = MakeAcademicYear(sessionStartYear - 1, isCurrent: false);
        var currentAcademicYear = MakeAcademicYear(sessionStartYear, isCurrent: true);
        _context.AcademicYears.AddRange(previousAcademicYear, currentAcademicYear);

        // ── Classes (8): grade 7..10 × sections A/B ───────────────────────────────
        // Stored flat in a grade-major map so offerings and student placement can look
        // them up by (grade, section) without hunting through an array.
        var classByGradeSection = new Dictionary<(int Grade, string Section), Class>();
        foreach (var grade in Grades)
        {
            foreach (var section in Sections)
            {
                var klass = Class.Create(grade, section);
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
        //   • General Math      — lower grades (7–8)
        //   • General Science   — lower grades (7–8), single combined science course
        //   • Higher Mathematics— upper grades (9–10)
        //   • Physics / Chemistry / Biology — upper grades (9–10) only
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

        // ── Teachers (5, including the demo login) ────────────────────────────────
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
        };
        var teachers = teacherDefs
            .Select(t => ApplicationUser.Create(t.Email, t.Name, passwordHash, Role.Teacher, teacherId: NextTeacherId()))
            .ToArray();
        _context.Users.AddRange(teachers);

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

        // One seat is the demo login, placed in grade 10 section A so the documented
        // `student@assignment.test` account lands in the senior-most class.
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
                    // Class X-A's first seat is the documented demo login.
                    var isDemo = grade == SeniorGrade && section == "A" && i == 0;
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
        var jane = students.Single(s => s.Email.Value == StudentEmail); // demo login

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

        // ── Course offerings: each grade studies only the subjects that fit its stage ─
        //   • Lower grades (7–8): Bangla, English, General Math, General Science          (4)
        //   • Upper grades (9–10): Physics, Chemistry, Higher Mathematics, Biology,
        //                          Bangla, English                                        (6)
        // The pure-science trio (Physics/Chemistry/Biology) and Higher Mathematics are
        // upper-grade-only; lower grades take General Math and the combined General Science.
        Course[] SubjectsForGrade(int grade) => grade <= 8
            ? [bangla, english, generalMath, generalScience]
            : [physics, chemistry, higherMath, biology, bangla, english];

        // Built in a stable order (grade → section → course) so the round-robin teacher
        // assignment below is deterministic across runs. The (grade, section, course) key is
        // carried alongside so the deliberately-unmapped five can be recognised by name rather
        // than by a positional index that would silently move if the plan above changed.
        var offerings = new List<(ClassCourse Offering, int Grade, string Section, string CourseCode)>();
        foreach (var grade in Grades)
        {
            foreach (var section in Sections)
            {
                var klass = classByGradeSection[(grade, section)];
                foreach (var course in SubjectsForGrade(grade))
                {
                    offerings.Add((ClassCourse.Create(klass.Id, course.Id), grade, section, course.Code));
                }
            }
        }

        _context.ClassCourses.AddRange(offerings.Select(o => o.Offering));
        await _context.SaveChangesAsync(ct);

        // ── Teaching mappings: round-robin the 5 teachers across the mapped offerings ─
        // Five offerings are skipped on purpose (see UnassignedOfferings) so the admin has
        // genuine mapping work to do; the remaining 35 divide evenly, 7 per teacher.
        var unassigned = UnassignedOfferings.ToHashSet();
        var teacherAssignments = new List<TeacherAssignment>();
        var mappingCursor = 0;
        foreach (var (offering, grade, section, courseCode) in offerings)
        {
            if (unassigned.Contains((grade, section, courseCode)))
            {
                continue;
            }

            var teacher = teachers[mappingCursor++ % teachers.Length];
            teacherAssignments.Add(TeacherAssignment.Create(teacher.Id, offering.Id));
        }

        _context.TeacherAssignments.AddRange(teacherAssignments);
        await _context.SaveChangesAsync(ct);

        // ── Assignments: one authored assignment per course, hosted in Class X-A ───
        // Authored by whichever teacher is mapped to that offering (the rule
        // CreateAssignmentHandler enforces). Mostly Published, one Draft so students see a
        // populated dashboard with at least one hidden assignment. Deadlines are offsets
        // from `now`; rule X5 requires deadline ≥ now + 1h, honoured by the SeederClock.
        var seniorClass = classByGradeSection[(SeniorGrade, "A")];
        Assignment MakeAssignment(Course course, string title, string description,
            TimeSpan untilDeadline, decimal maxMarks, bool publish)
        {
            var offering = offerings.First(o => o.Offering.ClassId == seniorClass.Id && o.Offering.CourseId == course.Id).Offering;
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

        var seniorStudents = studentPlacements
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
        };

        // One more, hosted in Class X-B with a near deadline, to seed a Late submission.
        var nearClass = classByGradeSection[(SeniorGrade, "B")];
        var nearOffering = offerings.First(o => o.Offering.ClassId == nearClass.Id && o.Offering.CourseId == higherMath.Id).Offering;
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
        var nearClassStudents = studentPlacements
            .Where(p => p.Class.Id == nearClass.Id)
            .Select(p => p.Student)
            .ToList();
        var lateStudent = nearClassStudents[0];

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

        // Class X-A assignments — the demo student and a couple of classmates.
        //   [0]=Bangla(20) [1]=English(20) [2]=HigherMath(50) [3]=Physics(50)
        //   [4]=Chemistry(30) [5]=Biology(25, DRAFT — no submissions)
        SubmitAndGrade(assignments[0], jane, "আমার প্রিয় ঋতু বর্ষা — প্রকৃতি তখন সবুজে ভরে ওঠে।", TimeSpan.FromDays(2), TimeSpan.FromHours(6), 18m, "সুন্দর লেখা হয়েছে, ভাষা প্রাঞ্জল।");
        Submit(assignments[1], jane, "In progress — outlined the causes, still writing the effects section.", TimeSpan.FromHours(3), finalize: false);
        Submit(assignments[1], seniorStudents[4], "Final draft attached — 420 words covering causes, effects and mitigation.", TimeSpan.FromDays(1), finalize: true);
        SubmitAndGrade(assignments[2], seniorStudents[1], "Differentiated problems 1 through 8; attached working for each step.", TimeSpan.FromDays(1), TimeSpan.FromHours(4), 42m, "Great work — small slip in problem 4.");
        Submit(assignments[3], seniorStudents[2], "Completed the first two problem sets; stuck on the third.", TimeSpan.FromHours(20), finalize: false);
        SubmitAndGrade(assignments[4], seniorStudents[3], "Answered all 15 questions on periodic trends and element classification.", TimeSpan.FromHours(8), TimeSpan.FromHours(2), 26m, "Solid grasp of periodic trends — well done.");

        // Class X-B late submission.
        Submit(aLate, lateStudent, "Submitting a little late — solved all the integration problems.", TimeSpan.FromMinutes(-75), finalize: true);

        _context.Submissions.AddRange(submissions);

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seed complete: {Classes} classes, {Courses} courses, {Offerings} offerings, {Teachers} teachers, " +
            "{Students} students, {Enrollments} enrollments, {TeacherAssignments} teaching mappings " +
            "({UnmappedOfferings} offerings left unmapped on purpose), " +
            "{Assignments} assignments ({PublishedAssignments} published), {Submissions} submissions. " +
            "Demo logins — admin={Admin}, teacher={Teacher}, student={Student}",
            classes.Length, courses.Length, offerings.Count, teachers.Length,
            students.Count, enrollments.Count, teacherAssignments.Count,
            offerings.Count - teacherAssignments.Count,
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
