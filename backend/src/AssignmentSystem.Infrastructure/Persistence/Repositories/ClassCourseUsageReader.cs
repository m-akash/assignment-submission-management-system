using AssignmentSystem.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Counts teaching mappings and assignments per offering. Two grouped queries for a whole
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

        var teacherCounts = await _context.TeacherAssignments
            .Where(ta => classCourseIds.Contains(ta.ClassCourseId))
            .GroupBy(ta => ta.ClassCourseId)
            .Select(g => new { ClassCourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassCourseId, x => x.Count, ct);

        // Draft assignments count too: the point of the number is "is anything relying on
        // this offering?", and the soft-delete query filter already excludes deleted ones.
        var assignmentCounts = await _context.Assignments
            .Where(a => classCourseIds.Contains(a.ClassCourseId))
            .GroupBy(a => a.ClassCourseId)
            .Select(g => new { ClassCourseId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClassCourseId, x => x.Count, ct);

        // Only ids that actually have something are returned — callers default the rest to zero.
        return classCourseIds
            .Where(id => teacherCounts.ContainsKey(id) || assignmentCounts.ContainsKey(id))
            .ToDictionary(
                id => id,
                id => new ClassCourseUsage(
                    teacherCounts.GetValueOrDefault(id),
                    assignmentCounts.GetValueOrDefault(id)));
    }
}
