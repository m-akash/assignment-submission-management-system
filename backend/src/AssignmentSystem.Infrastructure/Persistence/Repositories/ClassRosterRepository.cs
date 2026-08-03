using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Class-scoped queries over <c>ApplicationUser</c> that don't fit the generic
/// Specification pattern: counting students per class with a single grouped SQL query
/// (SELECT class_id, COUNT(*) ... GROUP BY class_id) instead of loading every student
/// row or querying once per class, and issuing the next student-id sequence number.
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

        var counts = await _context.Users
            .Where(u => u.Role == Role.Student && u.ClassId != null && classIds.Contains(u.ClassId.Value))
            .GroupBy(u => u.ClassId!.Value)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return counts.ToDictionary(x => x.ClassId, x => x.Count);
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
