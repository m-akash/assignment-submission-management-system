using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Roster queries that don't fit the generic Specification pattern: counting students per
/// class with a single grouped SQL query instead of loading every enrollment row, resolving
/// a student's classes for the rule-B1 checks, and issuing the next student-id sequence
/// number.
///
/// Every read here joins through to the student and filters on their account state. The
/// enrollment row is a link, so it says nothing about whether the person on the end of it is
/// still a live user — an enrollment belonging to a soft-deleted student must not be counted
/// in a class total or mailed about.
/// </summary>
internal sealed class ClassRosterRepository : IClassRosterRepository
{
    private readonly AppDbContext _context;

    public ClassRosterRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyDictionary<Guid, int>> GetStudentCountsAsync(
        IReadOnlyCollection<Guid> classIds, CancellationToken ct = default)
    {
        if (classIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        // SELECT class_id, COUNT(*) ... GROUP BY class_id — one round trip for the page.
        // The Student navigation is subject to the soft-delete query filter on users, so
        // enrollments whose student is gone drop out of the join automatically.
        var counts = await _context.StudentEnrollments
            .Where(e => classIds.Contains(e.ClassId) && e.Student.Role == Role.Student)
            .GroupBy(e => e.ClassId)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(x => x.ClassId, x => x.Count);
    }

    public async Task<IReadOnlyList<Guid>> GetEnrolledClassIdsAsync(Guid studentId, CancellationToken ct = default)
    {
        return await _context.StudentEnrollments
            .Where(e => e.StudentId == studentId)
            .Select(e => e.ClassId)
            .ToListAsync(ct);
    }

    public async Task<bool> IsEnrolledAsync(Guid studentId, Guid classId, CancellationToken ct = default)
    {
        return await _context.StudentEnrollments
            .AnyAsync(e => e.StudentId == studentId && e.ClassId == classId, ct);
    }

    public async Task<IReadOnlyList<NotificationRecipient>> GetClassRecipientsAsync(
        Guid classId, CancellationToken ct = default)
    {
        // IsActive as well as the implicit soft-delete filter: a deactivated student cannot
        // log in to act on the assignment, so mailing them is noise.
        var recipients = await _context.StudentEnrollments
            .Where(e => e.ClassId == classId && e.Student.Role == Role.Student && e.Student.IsActive)
            .Select(e => new { e.StudentId, Email = e.Student.Email.Value, e.Student.FullName })
            .Distinct()
            .ToListAsync(ct);

        return recipients
            .Select(r => new NotificationRecipient(r.StudentId, r.Email, r.FullName))
            .ToList();
    }

    public async Task<int> GetNextStudentSequenceAsync(string studentIdPrefix, CancellationToken ct = default)
    {
        // Match on the prefix plus its separator ("IX-A-") so "IX-A" cannot also pick up
        // a hypothetical "IX-AB-001". Pulling just the id column for one grade+section
        // and parsing the suffix in memory is cheaper and far less fragile than getting
        // Postgres to parse it via string functions.
        // IgnoreQueryFilters(): a soft-deleted student's number must never be reissued.
        var match = $"{studentIdPrefix}-";

        var studentIds = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.StudentId != null && u.StudentId.StartsWith(match))
            .Select(u => u.StudentId!)
            .ToListAsync(ct);

        var highestIssued = studentIds.Select(ExtractSequence).DefaultIfEmpty(0).Max();
        return highestIssued + 1;
    }

    private static int ExtractSequence(string studentId)
    {
        var lastDash = studentId.LastIndexOf('-');
        return lastDash >= 0 && int.TryParse(studentId[(lastDash + 1)..], out var sequence) ? sequence : 0;
    }
}
