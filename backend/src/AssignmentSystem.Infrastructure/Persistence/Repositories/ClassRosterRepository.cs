using AssignmentSystem.Application.Abstractions;
using AssignmentSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Counts students per class with a single grouped SQL query (SELECT class_id,
/// COUNT(*) ... GROUP BY class_id) instead of loading every student row or querying
/// once per class. The soft-delete global query filter on <c>ApplicationUser</c>
/// already excludes deactivated-and-deleted accounts from this count.
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
}
