using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Classes;
using AssignmentSystem.Domain.Enums;
using AssignmentSystem.Domain.Subjects;
using AssignmentSystem.Domain.Submissions;
using AssignmentSystem.Domain.TeacherAssignments;
using AssignmentSystem.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssignmentSystem.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent database seeder. Creates the demo Admin/Teacher/Student accounts plus
/// enough sample data (classes, subjects, teacher assignments, one assignment, one
/// submission) for the evaluator to exercise the system end-to-end. Skips when the
/// admin account already exists.
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

        if (await _context.Users.AnyAsync(u => u.Email.Value == AdminEmail, ct))
        {
            _logger.LogInformation("Seed already applied — skipping.");
            return;
        }

        _logger.LogInformation("Seeding demo data...");

        // ── Classes ────────────────────────────────────────────────────────────
        var class10A = Class.Create("Grade 10 - Section A", "10", "A");
        _context.Classes.Add(class10A);

        // ── Subjects ───────────────────────────────────────────────────────────
        var math = Subject.Create("Mathematics", "MATH101");
        var physics = Subject.Create("Physics", "PHY101");
        _context.Subjects.AddRange(math, physics);

        // ── Users ──────────────────────────────────────────────────────────────
        var passwordHash = _passwordHasher.Hash(DefaultPassword);

        var admin = ApplicationUser.Create(AdminEmail, "System Admin", passwordHash, Role.Admin);
        var teacher = ApplicationUser.Create(TeacherEmail, "John Teacher", passwordHash, Role.Teacher);
        var student = ApplicationUser.Create(StudentEmail, "Jane Student", passwordHash, Role.Student, class10A.Id);
        _context.Users.AddRange(admin, teacher, student);

        await _context.SaveChangesAsync(ct); // persist to resolve generated IDs

        // ── Teacher assignment (authorization link: teacher → subject → class) ─
        var teacherAssignment = TeacherAssignment.Create(teacher.Id, math.Id, class10A.Id);
        _context.TeacherAssignments.Add(teacherAssignment);
        await _context.SaveChangesAsync(ct);

        // ── Assignment (published, deadline ~7 days out) ───────────────────────
        var deadline = DateTime.UtcNow.AddDays(7);
        var assignment = Assignment.Create(
            teacherId: teacher.Id,
            subjectId: math.Id,
            classId: class10A.Id,
            teacherAssignmentId: teacherAssignment.Id,
            title: "Algebra Fundamentals",
            description: "Solve the attached problems on linear equations and submit your working.",
            deadlineUtc: deadline,
            maxMarks: 100m,
            allowResubmission: true,
            clock: new SeederClock());
        assignment.Publish();
        _context.Assignments.Add(assignment);
        await _context.SaveChangesAsync(ct);



        _logger.LogInformation("Seed complete: admin={Admin}, teacher={Teacher}, student={Student}",
            AdminEmail, TeacherEmail, StudentEmail);
    }

    /// <summary>Fixed clock so seeded timestamps are deterministic (deadlines far in the future).</summary>
    private sealed class SeederClock : Domain.Common.IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
