using AssignmentSystem.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>
/// Counts enrollments per academic year in one grouped query for a whole page.
/// See <see cref="IAcademicYearUsageReader"/> for why soft-deleted students are counted.
/// </summary>
internal sealed class AcademicYearUsageReader : IAcademicYearUsageReader
{
    private readonly AppDbContext _context;

    public AcademicYearUsageReader(AppDbContext context) => _context = context;

    public async Task<IReadOnlyDictionary<Guid, int>> GetEnrollmentCountsAsync(
        IReadOnlyCollection<Guid> academicYearIds, CancellationToken ct = default)
    {
        if (academicYearIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        // No join to the student, so the soft-delete query filter on users never applies —
        // which is what this count needs. See the port's remarks.
        return await _context.StudentEnrollments
            .Where(e => academicYearIds.Contains(e.AcademicYearId))
            .GroupBy(e => e.AcademicYearId)
            .Select(g => new { AcademicYearId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AcademicYearId, x => x.Count, ct);
    }
}
