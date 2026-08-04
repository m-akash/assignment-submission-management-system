using AssignmentSystem.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace AssignmentSystem.Infrastructure.Persistence.Repositories;

/// <summary>The Teacher-side equivalent of <see cref="ClassRosterRepository"/>.</summary>
internal sealed class TeacherRosterRepository : ITeacherRosterRepository
{
    private readonly AppDbContext _context;

    public TeacherRosterRepository(AppDbContext context) => _context = context;

    public async Task<int> GetNextTeacherSequenceAsync(CancellationToken ct = default)
    {
        // IgnoreQueryFilters(): a soft-deleted teacher's number must never be reissued.
        var teacherIds = await _context.Users
            .IgnoreQueryFilters()
            .Where(u => u.TeacherId != null)
            .Select(u => u.TeacherId!)
            .ToListAsync(ct);

        var highestIssued = teacherIds.Select(ExtractSequence).DefaultIfEmpty(0).Max();
        return highestIssued + 1;
    }

    private static int ExtractSequence(string teacherId)
    {
        var lastDash = teacherId.LastIndexOf('-');
        return lastDash >= 0 && int.TryParse(teacherId[(lastDash + 1)..], out var sequence) ? sequence : 0;
    }
}
