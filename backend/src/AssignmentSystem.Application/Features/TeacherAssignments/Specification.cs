using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.TeacherAssignments;

namespace AssignmentSystem.Application.Features.TeacherAssignments;

/// <summary>
/// Every <see cref="TeacherAssignment"/> row for one teacher. Reused beyond this feature
/// (e.g. by <c>GetEnrollmentsHandler</c>) to resolve the classes a teacher may see, so it
/// is public where the other specifications here are internal.
/// </summary>
public sealed class TeacherAssignmentsByTeacherSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentsByTeacherSpecification(Guid teacherId)
    {
        ApplyNoTracking();
        // ClassCourse is included so reuse sites (e.g. resolving the classes a teacher may
        // see enrollments for) get the class id without a second query.
        AddInclude(ta => ta.ClassCourse);
        Criteria = ta => ta.TeacherId == teacherId;
    }
}

internal sealed class TeacherAssignmentWithDetailsSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentWithDetailsSpecification(Guid id)
    {
        Criteria = ta => ta.Id == id;
        AddInclude(ta => ta.Teacher);
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
    }
}

/// <summary>
/// The one assignment (if any) already covering an offering. An offering may have at most
/// one teacher, so this both finds a pre-existing mapping to compare teachers against and
/// backs the "already has a teacher" duplicate check — there is no need for a
/// teacher-scoped variant since the offering alone now determines uniqueness.
/// </summary>
internal sealed class TeacherAssignmentByClassCourseSpecification : Specification<TeacherAssignment>
{
    public TeacherAssignmentByClassCourseSpecification(Guid classCourseId)
    {
        Criteria = ta => ta.ClassCourseId == classCourseId;
    }
}

internal sealed class TeacherAssignmentsPagedSpecification : Specification<TeacherAssignment>
{
    /// <summary>Columns this endpoint may be sorted by. See <see cref="SortMap{T}"/>.</summary>
    private static readonly SortMap<TeacherAssignment> Sortable = new(
        new Dictionary<string, System.Linq.Expressions.Expression<Func<TeacherAssignment, object>>>
        {
            ["teacher"] = ta => ta.Teacher.FullName,
            ["course"] = ta => ta.ClassCourse.Course.Name,
            ["class"] = ta => ta.ClassCourse.Class.Level,
            ["createdAt"] = ta => ta.CreatedAtUtc,
        },
        tieBreaker: ta => ta.Id);

    public TeacherAssignmentsPagedSpecification(
        IEnumerable<Guid>? teacherIds, IEnumerable<Guid>? courseIds, IEnumerable<Guid>? classIds,
        IEnumerable<Guid>? classCourseIds, string? search,
        string? sortBy, string? sortDir, int page, int pageSize)
    {
        ApplyNoTracking();
        AddInclude(ta => ta.Teacher);
        AddInclude("ClassCourse.Class");
        AddInclude("ClassCourse.Course");
        if (!ApplySort(Sortable, sortBy, sortDir))
        {
            ApplyOrderBy(ta => ta.Teacher.FullName);
        }
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();
        var teacherFilter = MultiValueFilter(teacherIds);
        var classCourseFilter = MultiValueFilter(classCourseIds);
        var courseFilter = MultiValueFilter(courseIds);
        var classFilter = MultiValueFilter(classIds);

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
#pragma warning disable CA1304, CA1311
        Criteria = ta =>
            (teacherFilter == null || teacherFilter.Contains(ta.TeacherId)) &&
            (classCourseFilter == null || classCourseFilter.Contains(ta.ClassCourseId)) &&
            (courseFilter == null || courseFilter.Contains(ta.ClassCourse.CourseId)) &&
            (classFilter == null || classFilter.Contains(ta.ClassCourse.ClassId)) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             ta.Teacher.FullName.ToLower().Contains(searchLower) ||
             ta.ClassCourse.Course.Name.ToLower().Contains(searchLower) ||
             ta.ClassCourse.Class.Name.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
