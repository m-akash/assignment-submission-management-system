using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.Assignments;
using AssignmentSystem.Domain.Enums;

namespace AssignmentSystem.Application.Features.Assignments;

/// <summary>
/// An assignment with everything the DTO and the authorization checks need. The offering
/// is included two levels deep because the class and course names live on the far side of
/// it, and because rule B1 needs the offering's class id to test enrollment.
/// </summary>
internal sealed class AssignmentWithDetailsSpecification : Specification<Assignment>
{
    public AssignmentWithDetailsSpecification(Guid id)
    {
        Criteria = a => a.Id == id;
        AddInclude(a => a.Teacher);
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
        AddInclude(a => a.Files);
    }
}

/// <summary>
/// The scope of an assignment without its files — for the write paths that need to answer
/// "which class and course is this for?" before they mutate something, and for composing
/// notification bodies.
/// </summary>
internal sealed class AssignmentWithScopeSpecification : Specification<Assignment>
{
    public AssignmentWithScopeSpecification(Guid id)
    {
        Criteria = a => a.Id == id;
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
    }
}

internal sealed class AssignmentsPagedSpecification : Specification<Assignment>
{
    /// <summary>Columns this endpoint may be sorted by. See <see cref="SortMap{T}"/>.</summary>
    private static readonly SortMap<Assignment> Sortable = new(
        new Dictionary<string, System.Linq.Expressions.Expression<Func<Assignment, object>>>
        {
            ["title"] = a => a.Title,
            ["deadline"] = a => a.DeadlineUtc,
            ["maxMarks"] = a => a.MaxMarks,
            ["status"] = a => a.Status,
            ["createdAt"] = a => a.CreatedAtUtc,
        },
        tieBreaker: a => a.Id);

    public AssignmentsPagedSpecification(
        IEnumerable<Guid>? classIds,
        IEnumerable<Guid>? courseIds,
        IEnumerable<Guid>? classCourseIds,
        IReadOnlyList<Guid>? restrictToClassIds,
        IEnumerable<Guid>? teacherIds,
        IEnumerable<AssignmentStatus>? statuses,
        string? search,
        string? sortBy,
        string? sortDir,
        int page,
        int pageSize)
    {
        ApplyNoTracking();
        AddInclude(a => a.Teacher);
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
        AddInclude(a => a.Files);
        if (!ApplySort(Sortable, sortBy, sortDir))
        {
            ApplyOrderByDescending(a => a.CreatedAtUtc);
        }
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();
        var classFilter = MultiValueFilter(classIds);
        var courseFilter = MultiValueFilter(courseIds);
        var classCourseFilter = MultiValueFilter(classCourseIds);
        var teacherFilter = MultiValueFilter(teacherIds);
        var statusFilter = MultiValueFilter(statuses);

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
        //
        // restrictToClassIds is the student scope (rule B1): the classes they are enrolled
        // in. A student with no enrollment must see nothing, so an EMPTY list has to match
        // nothing — which is why it is used raw rather than through MultiValueFilter, whose
        // whole job is to turn an empty caller-supplied filter back into "no filter".
#pragma warning disable CA1304, CA1311
        Criteria = a =>
            (classCourseFilter == null || classCourseFilter.Contains(a.ClassCourseId)) &&
            (classFilter == null || classFilter.Contains(a.ClassCourse.ClassId)) &&
            (courseFilter == null || courseFilter.Contains(a.ClassCourse.CourseId)) &&
            (restrictToClassIds == null || restrictToClassIds.Contains(a.ClassCourse.ClassId)) &&
            (teacherFilter == null || teacherFilter.Contains(a.TeacherId)) &&
            (statusFilter == null || statusFilter.Contains(a.Status)) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             a.Title.ToLower().Contains(searchLower) ||
             // DescriptionText, not Description: the description is markup, and matching
             // against it would turn a search for "li" into "every assignment containing a
             // list". The database keeps the stripped copy in step with the original.
             a.DescriptionText.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
