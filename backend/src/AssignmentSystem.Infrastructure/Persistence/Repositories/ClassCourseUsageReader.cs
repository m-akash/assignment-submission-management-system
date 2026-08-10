using AssignmentSystem.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Reads assigned teachers and assignment counts per offering. Two queries for a whole
/// page, merged in memory — the alternative (a subquery per row, or loading the rows being
/// counted) is what this port exists to avoid.
/// </summary>
internal sealed class ClassCourseUsageReader : IClassCourseUsageReader
{
    private readonly AppDbContext _context;

    public ClassCourseUsageReader(AppDbContext context) => _context = context;

    public async Task<IReadOnlyDictionary<Guid, ClassCourseUsage>> GetUsageAsync(
        IReadOnlyCollection<Guid> classCourseIds, CancellationToken ct = default)
    {
        if (classCourseIds.Count == 0)
        {
            return new Dictionary<Guid, ClassCourseUsage>();
        }

        // The names, not just a count: a screen listing offerings shows who teaches each one,
        // and the count it also needs is the length of that list. Grouping happens in memory
        // because the rows are wanted, so grouping in SQL would save nothing.
        // A mapping outlives the soft-delete of its teacher, and a deleted teacher is not one
        // this offering has — excluded explicitly so the count cannot disagree with the names.
        var teacherRows = await _context.TeacherAssignments
            .Where(ta => classCourseIds.Contains(ta.ClassCourseId) && !ta.Teacher.IsDeleted)
            .OrderBy(ta => ta.Teacher.FullName)
            .Select(ta => new { ta.ClassCourseId, ta.Teacher.FullName })
            .ToListAsync(ct);

        var teachersByOffering = teacherRows
            .GroupBy(row => row.ClassCourseId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(row => row.FullName).ToList());

        // Draft assignments count too: the point of the number is "is anything relying on
        // this offering?", and the soft-delete query filter already excludes deleted ones.
        var assignmentCounts = await _context.Assignments
            .Where(a => classCourseIds.Contains(a.ClassCourseId))
            .GroupBy(a => a.ClassCourseId)
            .Select(g => new { ClassCourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassCourseId, x => x.Count, ct);

        // Only ids that actually have something are returned — callers default the rest to zero.
        return classCourseIds
            .Where(id => teachersByOffering.ContainsKey(id) || assignmentCounts.ContainsKey(id))
            .ToDictionary(
                id => id,
                id =>
                {
                    var teachers = teachersByOffering.GetValueOrDefault(id, []);
                    return new ClassCourseUsage(
                        teachers.Count,
                        assignmentCounts.GetValueOrDefault(id),
                        teachers);
                });
    }
}
