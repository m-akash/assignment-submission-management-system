namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Counts what hangs off a set of course offerings — teaching mappings and assignments —
/// in two grouped queries for a whole page rather than two per row. A specific port for
/// the same reason <see cref="IClassRosterRepository"/> is one: an aggregate over related
/// tables is not expressible as a Specification without loading the rows it is counting.
/// </summary>
public interface IClassCourseUsageReader
{
    /// <summary>
    /// Teaching-mapping and assignment counts per offering, for exactly the given ids.
    /// Offerings with neither are absent from the result — callers should default to zero.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, ClassCourseUsage>> GetUsageAsync(
        IReadOnlyCollection<Guid> classCourseIds, CancellationToken ct = default);
}

/// <summary>
/// A struct so <c>GetValueOrDefault</c> on a missing offering yields (0, 0) rather than
/// null — "nothing hangs off it yet" is the correct reading of an absent row.
/// </summary>
public readonly record struct ClassCourseUsage(int TeacherCount, int AssignmentCount);
