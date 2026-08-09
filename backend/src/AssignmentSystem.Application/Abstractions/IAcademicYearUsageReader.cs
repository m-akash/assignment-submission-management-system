namespace AssignmentSystem.Application.Abstractions;

/// <summary>
/// Counts the enrollments recorded against a set of academic years — one grouped query for
/// a whole page rather than one per row. A specific port for the same reason
/// <see cref="IClassCourseUsageReader"/> is one: an aggregate over a related table is not
/// expressible as a Specification without loading the rows it is counting.
/// </summary>
public interface IAcademicYearUsageReader
{
    /// <summary>
    /// Enrollment count per academic year, for exactly the given ids. Years with none are
    /// absent from the result — callers should default to zero.
    ///
    /// Counts every enrollment row, including those belonging to soft-deleted students.
    /// That is deliberate: this number is what the delete guard refuses on, and a
    /// soft-deleted student's row still holds the foreign key, so a count that skipped
    /// them would promise a delete the database would then reject.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetEnrollmentCountsAsync(
        IReadOnlyCollection<Guid> academicYearIds, CancellationToken ct = default);
}
