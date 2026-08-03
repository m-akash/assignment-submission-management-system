using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Courses;
using AssignmentSystem.Domain.Departments;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssignmentSystem.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent database seeder. Creates the demo Admin/Teacher/Student accounts (the
/// ones documented in the README) plus a wider sample dataset — 10 classes, 12
/// courses, 12 teachers, 15 students, 15 teacher-assignments, 15 assignments and 15
/// submissions spread across every status — so an evaluator sees a populated system
/// immediately instead of three empty accounts. Skips entirely once the admin account
/// already exists.
/// </summary>
public sealed class DbSeeder
{
    public const string AdminEmail = "admin@assignment.test";
    public const string TeacherEmail = "teacher@assignment.test";
    public const string StudentEmail = "student@assignment.test";

    // Demo passwords — documented in README. These are for local/demo only.
    public const string DefaultPassword = "Password123!";

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

        // ── Classes (10) ──────────────────────────────────────────────────────────
        // Grades are Roman numerals by school convention ("Class IX"), and student ids
        // are built straight from grade + section — so "IX-A-001".
        var classes = new[]
        {
            Class.Create("Class VI - Section A", "VI", "A"),
            Class.Create("Class VI - Section B", "VI", "B"),
            Class.Create("Class VII - Section A", "VII", "A"),
            Class.Create("Class VII - Section B", "VII", "B"),
            Class.Create("Class VIII - Section A", "VIII", "A"),
            Class.Create("Class VIII - Section B", "VIII", "B"),
            Class.Create("Class IX - Section A", "IX", "A"),
            Class.Create("Class IX - Section B", "IX", "B"),
            Class.Create("Class X - Section A", "X", "A"),
            Class.Create("Class X - Section B", "X", "B"),
        };
        _context.Classes.AddRange(classes);
        var class6A = classes[0];
        var class6B = classes[1];
        var class7A = classes[2];
        var class7B = classes[3];
        var class8A = classes[4];
        var class8B = classes[5];
        var class9A = classes[6];
        var class9B = classes[7];
        var class10A = classes[8];
        var class10B = classes[9];

        // ── Departments (5) ───────────────────────────────────────────────────────
        // One department owns many courses — Science covers Physics, Chemistry, Biology.
        var departments = new[]
        {
            Department.Create("Science", "SCI"),
            Department.Create("Mathematics", "MATH"),
            Department.Create("Languages", "LANG"),
            Department.Create("Humanities", "HUM"),
            Department.Create("Business & ICT", "BUS"),
        };
        _context.Departments.AddRange(departments);
        var scienceDept = departments[0];
        var mathsDept = departments[1];
        var languagesDept = departments[2];
        var humanitiesDept = departments[3];
        var businessDept = departments[4];

        // ── Courses (12) ─────────────────────────────────────────────────────────
        var courses = new[]
        {
            Course.Create("Mathematics", "MATH101", mathsDept.Id),
            Course.Create("Physics", "PHY101", scienceDept.Id),
            Course.Create("Chemistry", "CHE101", scienceDept.Id),
            Course.Create("Biology", "BIO101", scienceDept.Id),
            Course.Create("English", "ENG101", languagesDept.Id),
            Course.Create("Bangla", "BAN101", languagesDept.Id),
            Course.Create("ICT", "ICT101", businessDept.Id),
            Course.Create("History", "HIS101", humanitiesDept.Id),
            Course.Create("Geography", "GEO101", humanitiesDept.Id),
            Course.Create("Economics", "ECO101", businessDept.Id),
            Course.Create("Accounting", "ACC101", businessDept.Id),
            Course.Create("Higher Mathematics", "HMT101", mathsDept.Id),
        };
        _context.Courses.AddRange(courses);
        var math = courses[0];
        var physics = courses[1];
        var chemistry = courses[2];
        var biology = courses[3];
        var english = courses[4];
        var bangla = courses[5];
        var ict = courses[6];
        var history = courses[7];
        var geography = courses[8];
        var economics = courses[9];
        var accounting = courses[10];
        var higherMath = courses[11];

        // ── Users: teachers (12, including the demo login) ──────────────────────────
        // Mirrors the production rule in CreateUserHandler: "INS-{department code}-{sequence}",
        // sequence numbers restarting at 1 per department.
        var teacherSequence = new Dictionary<Guid, int>();
        string NextTeacherId(Department department)
        {
            var sequence = teacherSequence.GetValueOrDefault(department.Id, 0) + 1;
            teacherSequence[department.Id] = sequence;
            return $"INS-{department.Code}-{sequence:D2}";
        }

        var teachers = new[]
        {
            ApplicationUser.Create(TeacherEmail, "John Teacher", passwordHash, Role.Teacher, departmentId: mathsDept.Id, teacherId: NextTeacherId(mathsDept)),
            ApplicationUser.Create("teacher2@assignment.test", "Sarah Rahman", passwordHash, Role.Teacher, departmentId: languagesDept.Id, teacherId: NextTeacherId(languagesDept)),
            ApplicationUser.Create("teacher3@assignment.test", "Kamal Hossain", passwordHash, Role.Teacher, departmentId: languagesDept.Id, teacherId: NextTeacherId(languagesDept)),
            ApplicationUser.Create("teacher4@assignment.test", "Nusrat Jahan", passwordHash, Role.Teacher, departmentId: scienceDept.Id, teacherId: NextTeacherId(scienceDept)),
            ApplicationUser.Create("teacher5@assignment.test", "Farhan Ahmed", passwordHash, Role.Teacher, departmentId: scienceDept.Id, teacherId: NextTeacherId(scienceDept)),
            ApplicationUser.Create("teacher6@assignment.test", "Rima Chowdhury", passwordHash, Role.Teacher, departmentId: businessDept.Id, teacherId: NextTeacherId(businessDept)),
            ApplicationUser.Create("teacher7@assignment.test", "Imran Khan", passwordHash, Role.Teacher, departmentId: humanitiesDept.Id, teacherId: NextTeacherId(humanitiesDept)),
            ApplicationUser.Create("teacher8@assignment.test", "Tania Islam", passwordHash, Role.Teacher, departmentId: humanitiesDept.Id, teacherId: NextTeacherId(humanitiesDept)),
            ApplicationUser.Create("teacher9@assignment.test", "Shakil Ahmed", passwordHash, Role.Teacher, departmentId: businessDept.Id, teacherId: NextTeacherId(businessDept)),
            ApplicationUser.Create("teacher10@assignment.test", "Mou Akter", passwordHash, Role.Teacher, departmentId: businessDept.Id, teacherId: NextTeacherId(businessDept)),
            ApplicationUser.Create("teacher11@assignment.test", "Rafiq Uddin", passwordHash, Role.Teacher, departmentId: mathsDept.Id, teacherId: NextTeacherId(mathsDept)),
            ApplicationUser.Create("teacher12@assignment.test", "Sabrina Yasmin", passwordHash, Role.Teacher, departmentId: scienceDept.Id, teacherId: NextTeacherId(scienceDept)),
        };
        _context.Users.AddRange(teachers);
        var johnTeacher = teachers[0];
        var sarah = teachers[1];
        var kamal = teachers[2];
        var nusrat = teachers[3];
        var farhan = teachers[4];
        var rima = teachers[5];
        var imran = teachers[6];
        var tania = teachers[7];
        var shakil = teachers[8];
        var mou = teachers[9];
        var rafiq = teachers[10];
        var sabrina = teachers[11];

        // ── Users: admin + students (15, including the demo login) ──────────────────
        var admin = ApplicationUser.Create(AdminEmail, "System Admin", passwordHash, Role.Admin);
        _context.Users.Add(admin);

        // Mirrors the production rule in CreateUserHandler: "{grade}-{section}-{sequence}"
        // with a Roman-numeral grade, sequence numbers restarting at 1 per grade+section.
        var studentSequence = new Dictionary<string, int>(StringComparer.Ordinal);
        string NextStudentId(Class classRoom)
        {
            var prefix = $"{classRoom.Grade}-{classRoom.Section}";
            var sequence = studentSequence.GetValueOrDefault(prefix, 0) + 1;
            studentSequence[prefix] = sequence;
            return $"{prefix}-{sequence:D3}";
        }

        var students = new[]
        {
            ApplicationUser.Create(StudentEmail, "Jane Student", passwordHash, Role.Student, class10A.Id, NextStudentId(class10A)),
            ApplicationUser.Create("student2@assignment.test", "Arif Hasan", passwordHash, Role.Student, class6A.Id, NextStudentId(class6A)),
            ApplicationUser.Create("student3@assignment.test", "Priya Sultana", passwordHash, Role.Student, class6A.Id, NextStudentId(class6A)),
            ApplicationUser.Create("student4@assignment.test", "Tanvir Alam", passwordHash, Role.Student, class6B.Id, NextStudentId(class6B)),
            ApplicationUser.Create("student5@assignment.test", "Nadia Islam", passwordHash, Role.Student, class7A.Id, NextStudentId(class7A)),
            ApplicationUser.Create("student6@assignment.test", "Omar Faruk", passwordHash, Role.Student, class7A.Id, NextStudentId(class7A)),
            ApplicationUser.Create("student7@assignment.test", "Lamia Akter", passwordHash, Role.Student, class7B.Id, NextStudentId(class7B)),
            ApplicationUser.Create("student8@assignment.test", "Zubair Rahman", passwordHash, Role.Student, class8A.Id, NextStudentId(class8A)),
            ApplicationUser.Create("student9@assignment.test", "Rakib Hossain", passwordHash, Role.Student, class8B.Id, NextStudentId(class8B)),
            ApplicationUser.Create("student10@assignment.test", "Sadia Islam", passwordHash, Role.Student, class9A.Id, NextStudentId(class9A)),
            ApplicationUser.Create("student11@assignment.test", "Mahin Khan", passwordHash, Role.Student, class9B.Id, NextStudentId(class9B)),
            ApplicationUser.Create("student12@assignment.test", "Farzana Rahman", passwordHash, Role.Student, class10A.Id, NextStudentId(class10A)),
            ApplicationUser.Create("student13@assignment.test", "Hasib Chowdhury", passwordHash, Role.Student, class10B.Id, NextStudentId(class10B)),
            ApplicationUser.Create("student14@assignment.test", "Ayesha Siddika", passwordHash, Role.Student, class10B.Id, NextStudentId(class10B)),
            ApplicationUser.Create("student15@assignment.test", "Kamrul Islam", passwordHash, Role.Student, class9A.Id, NextStudentId(class9A)),
        };
        _context.Users.AddRange(students);
        var jane = students[0];
        var arif = students[1];
        var priya = students[2];
        var tanvir = students[3];
        var nadia = students[4];
        var omar = students[5];
        var lamia = students[6];
        var zubair = students[7];
        var rakib = students[8];
        var sadia = students[9];
        var mahin = students[10];
        var farzana = students[11];
        var hasib = students[12];
        var ayesha = students[13];
        var kamrul = students[14];
        _ = (lamia, farzana); // seeded for class/user volume; not used in a scripted submission below

        await _context.SaveChangesAsync(ct); // persist to resolve generated IDs

        // ── Teacher assignments (15): who may teach/grade what, where ───────────────
        var teacherAssignments = new[]
        {
            TeacherAssignment.Create(johnTeacher.Id, math.Id, class10A.Id),
            TeacherAssignment.Create(johnTeacher.Id, physics.Id, class10A.Id),
            TeacherAssignment.Create(johnTeacher.Id, math.Id, class10B.Id),
            TeacherAssignment.Create(sarah.Id, english.Id, class6A.Id),
            TeacherAssignment.Create(kamal.Id, bangla.Id, class6B.Id),
            TeacherAssignment.Create(nusrat.Id, chemistry.Id, class9A.Id),
            TeacherAssignment.Create(farhan.Id, biology.Id, class9B.Id),
            TeacherAssignment.Create(rima.Id, ict.Id, class8A.Id),
            TeacherAssignment.Create(imran.Id, history.Id, class8B.Id),
            TeacherAssignment.Create(tania.Id, geography.Id, class7A.Id),
            TeacherAssignment.Create(shakil.Id, economics.Id, class7B.Id),
            TeacherAssignment.Create(mou.Id, accounting.Id, class10B.Id),
            TeacherAssignment.Create(rafiq.Id, higherMath.Id, class9A.Id),
            TeacherAssignment.Create(sabrina.Id, physics.Id, class9B.Id),
            TeacherAssignment.Create(nusrat.Id, chemistry.Id, class9B.Id),
        };
        _context.TeacherAssignments.AddRange(teacherAssignments);
        await _context.SaveChangesAsync(ct);

        // ── Assignments (15): mostly Published, two Draft (not visible to students) ──
        Assignment MakeAssignment(int taIndex, string title, string description, TimeSpan untilDeadline, decimal maxMarks, bool publish)
        {
            var ta = teacherAssignments[taIndex];
            var assignment = Assignment.Create(
                teacherId: ta.TeacherId,
                courseId: ta.CourseId,
                classId: ta.ClassId,
                teacherAssignmentId: ta.Id,
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

        var a1 = MakeAssignment(0, "Algebra Fundamentals", "Solve the attached problems on linear equations and submit your working.", TimeSpan.FromDays(7), 100m, publish: true);
        var a2 = MakeAssignment(1, "Newton's Laws Problem Set", "Answer the three problem sets covering Newton's first, second and third laws.", TimeSpan.FromDays(10), 50m, publish: true);
        var a3 = MakeAssignment(2, "Quadratic Equations Practice", "Complete the worksheet on solving quadratic equations by factoring and the formula.", TimeSpan.FromDays(5), 50m, publish: true);
        var a4 = MakeAssignment(3, "Essay: My Favourite Book", "Write a 500-word essay describing your favourite book and why you enjoyed it.", TimeSpan.FromDays(4), 20m, publish: true);
        var a5 = MakeAssignment(4, "রচনা: আমার প্রিয় শিক্ষক", "তোমার প্রিয় শিক্ষকের উপর একটি রচনা লেখো।", TimeSpan.FromDays(6), 20m, publish: true);
        var a6 = MakeAssignment(5, "Periodic Table Quiz", "Answer the short-answer quiz on periods, groups and element properties.", TimeSpan.FromDays(3), 30m, publish: true);
        var a7 = MakeAssignment(6, "Cell Structure Diagram", "Label the plant and animal cell diagrams and describe each organelle's function.", TimeSpan.FromDays(8), 25m, publish: false);
        var a8 = MakeAssignment(7, "HTML Basics Project", "Build a 3-page static website using semantic HTML.", TimeSpan.FromDays(12), 40m, publish: true);
        var a9 = MakeAssignment(8, "World War II Summary", "Summarize the causes and key events of World War II in your own words.", TimeSpan.FromDays(9), 30m, publish: true);
        var a10 = MakeAssignment(9, "Map Reading Exercise", "Identify the marked coordinates and physical features on the attached map.", TimeSpan.FromDays(2), 20m, publish: true);
        var a11 = MakeAssignment(10, "Supply and Demand Case Study", "Analyse the attached case study using supply and demand curves.", TimeSpan.FromDays(14), 35m, publish: false);
        var a12 = MakeAssignment(11, "Journal Entries Practice", "Record the given transactions as journal entries with narration.", TimeSpan.FromDays(6), 40m, publish: true);
        var a13 = MakeAssignment(12, "Calculus Introduction", "Solve the introductory differentiation problems.", TimeSpan.FromDays(11), 50m, publish: true);
        var a14 = MakeAssignment(13, "Kinematics Problems", "Solve the kinematics problems on the attached sheet. Due shortly — submit promptly.", TimeSpan.FromMinutes(65), 30m, publish: true);
        var a15 = MakeAssignment(14, "Chemical Bonding Worksheet", "Complete the worksheet on ionic and covalent bonding.", TimeSpan.FromDays(15), 25m, publish: true);

        _context.Assignments.AddRange([a1, a2, a3, a4, a5, a6, a7, a8, a9, a10, a11, a12, a13, a14, a15]);
        await _context.SaveChangesAsync(ct);

        // ── Submissions (15): Pending / Submitted / Graded / Late, across students ──
        // Non-Late timestamps are anchored to "now minus a few hours/days" so they read as
        // already-submitted no matter when this seed happens to run — every assignment's own
        // deadline is always further out than that (rule X5 requires deadlines in the future).
        // a14 is the one exception: its deadline is only ~65 minutes out, so its Late row is
        // dated ~75 minutes from now (10 minutes past that deadline) — briefly a "future"
        // timestamp, but the persisted Status is fixed as Late at write time regardless.
        void Submit(Assignment assignment, ApplicationUser student, string content, TimeSpan ago, bool finalize)
        {
            var submission = Submission.Create(assignment.Id, student.Id, content, hasFile: false, assignment, new FixedClock(now - ago), finalize);
            assignment.IncrementSubmissionCount();
            _context.Submissions.Add(submission);
        }

        void SubmitAndGrade(Assignment assignment, ApplicationUser student, string content, TimeSpan submittedAgo, TimeSpan gradedAgo, decimal marks, string feedback)
        {
            var submission = Submission.Create(assignment.Id, student.Id, content, hasFile: false, assignment, new FixedClock(now - submittedAgo), finalize: true);
            assignment.IncrementSubmissionCount();
            submission.Grade(marks, feedback, assignment.TeacherId, assignment, new FixedClock(now - gradedAgo));
            _context.Submissions.Add(submission);
        }

        SubmitAndGrade(a1, jane, "Solved all 10 linear equations; attached working for each step.", TimeSpan.FromDays(2), TimeSpan.FromHours(6), 85m, "Great work overall — small arithmetic slip in problem 4, otherwise clean working.");
        Submit(a2, jane, "In progress — completed the first-law problems, still working through the third.", TimeSpan.FromHours(3), finalize: false);
        SubmitAndGrade(a3, hasib, "Factored all six quadratics; used the formula for the last two.", TimeSpan.FromDays(1), TimeSpan.FromHours(4), 40m, "Good attempt — double-check the sign in question 5.");
        Submit(a4, arif, "My favourite book is Treasure Island, because of its sense of adventure and discovery.", TimeSpan.FromHours(20), finalize: true);
        SubmitAndGrade(a4, priya, "My favourite book is Charlotte's Web. It taught me about friendship and loyalty.", TimeSpan.FromDays(1), TimeSpan.FromHours(2), 18m, "Well written and thoughtful — lovely essay!");
        Submit(a5, tanvir, "আমার প্রিয় শিক্ষক আমাদের গণিত শিক্ষক, কারণ তিনি সবসময় সহজভাবে বুঝিয়ে দেন।", TimeSpan.FromHours(30), finalize: true);
        SubmitAndGrade(a6, sadia, "Answered all 15 questions on periodic trends and element classification.", TimeSpan.FromHours(15), TimeSpan.FromHours(1), 27m, "Correct concepts throughout — well done.");
        Submit(a8, zubair, "Uploaded my 3-page site covering the home, about and contact sections.", TimeSpan.FromDays(2), finalize: true);
        SubmitAndGrade(a9, rakib, "Covered the causes, major battles, and the end of the war with a timeline.", TimeSpan.FromDays(3), TimeSpan.FromHours(5), 25m, "Detailed and well organised summary.");
        Submit(a10, nadia, "Marked coordinates 1 through 5, still verifying the contour lines for the rest.", TimeSpan.FromHours(10), finalize: false);
        Submit(a10, omar, "Identified all marked coordinates and physical features on the map.", TimeSpan.FromHours(8), finalize: true);
        Submit(a12, ayesha, "Recorded all 8 transactions as journal entries with narration.", TimeSpan.FromDays(1), finalize: true);
        Submit(a13, kamrul, "Solved the differentiation problems 1 through 10.", TimeSpan.FromHours(12), finalize: true);
        Submit(a14, mahin, "Submitting a little late — solved all the kinematics problems.", TimeSpan.FromMinutes(-75), finalize: true);
        SubmitAndGrade(a15, mahin, "Completed the worksheet on ionic and covalent bonding with examples.", TimeSpan.FromHours(18), TimeSpan.FromHours(2), 20m, "Solid understanding of bonding types.");

        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Seed complete: {Classes} classes, {Courses} courses, {Teachers} teachers, {Students} students, " +
            "{TeacherAssignments} teacher-assignments, {Assignments} assignments, {Submissions} submissions. " +
            "Demo logins — admin={Admin}, teacher={Teacher}, student={Student}",
            classes.Length, courses.Length, teachers.Length, students.Length,
            teacherAssignments.Length, 15, 15,
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
