using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.AcademicYears;
using AssignmentSystem.Domain.Enrollments;

namespace AssignmentSystem.Application.Features.AcademicYears;

/// <summary>
/// The year already using a name, if any. Compared case-insensitively so "2026-27" cannot
/// slip past an existing "2026-27" typed with different casing in a future naming scheme.
/// </summary>
internal sealed class AcademicYearByNameSpecification : Specification<AcademicYear>
{
    public AcademicYearByNameSpecification(string name, Guid? excludingId = null)
    {
        var nameLower = name.Trim().ToLowerInvariant();
        var excluded = excludingId ?? Guid.Empty;

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
#pragma warning disable CA1304, CA1311
        Criteria = y => y.Name.ToLower() == nameLower && y.Id != excluded;
#pragma warning restore CA1304, CA1311
    }
}

/// <summary>
/// The year currently flagged as the school's session, if any. Tracked (no
/// <c>ApplyNoTracking</c>) because the handler clears the flag on whatever it finds.
/// </summary>
internal sealed class CurrentAcademicYearSpecification : Specification<AcademicYear>
{
    public CurrentAcademicYearSpecification(Guid? excludingId = null)
    {
        var excluded = excludingId ?? Guid.Empty;
        Criteria = y => y.IsCurrent && y.Id != excluded;
    }
}

/// <summary>Enrollments recorded against one year — the delete guard's question.</summary>
internal sealed class EnrollmentsByAcademicYearSpecification : Specification<StudentEnrollment>
{
    public EnrollmentsByAcademicYearSpecification(Guid academicYearId)
    {
        Criteria = e => e.AcademicYearId == academicYearId;
    }
}

internal sealed class AcademicYearsPagedSpecification : Specification<AcademicYear>
{
    /// <summary>Columns this endpoint may be sorted by. See <see cref="SortMap{T}"/>.</summary>
    private static readonly SortMap<AcademicYear> Sortable = new(
        new Dictionary<string, System.Linq.Expressions.Expression<Func<AcademicYear, object>>>
        {
            ["name"] = y => y.Name,
            ["startDate"] = y => y.StartDate,
            ["endDate"] = y => y.EndDate,
            ["createdAt"] = y => y.CreatedAtUtc,
        },
        tieBreaker: y => y.Id);

    public AcademicYearsPagedSpecification(string? search, string? sortBy, string? sortDir, int page, int pageSize)
    {
        ApplyNoTracking();
        if (!ApplySort(Sortable, sortBy, sortDir))
        {
            // Newest session first: an admin opening this screen is nearly always working on
            // the year about to start, not the ones the school has finished with. Sorting by
            // name would only agree with that by luck — the label is free text.
            ApplyOrderByDescending(y => y.StartDate);
        }
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = y =>
            string.IsNullOrWhiteSpace(searchLower) ||
            y.Name.ToLower().Contains(searchLower);
#pragma warning restore CA1304, CA1311
    }
}
