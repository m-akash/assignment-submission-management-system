using AssignmentSystem.Application.Common.Interfaces;
using AssignmentSystem.Domain.ClassCourses;

namespace AssignmentSystem.Application.Features.ClassCourses;

/// <summary>
/// An offering with both sides loaded. Used well beyond this feature — anything that
/// needs to render or describe an assignment's scope (including the notification bodies)
/// needs the class and course names, not just their ids.
/// </summary>
internal sealed class ClassCourseWithDetailsSpecification : Specification<ClassCourse>
{
    public ClassCourseWithDetailsSpecification(Guid id)
    {
        Criteria = cc => cc.Id == id;
        AddInclude(cc => cc.Class);
        AddInclude(cc => cc.Course);
    }
}

/// <summary>
/// Every offering a class studies, course side loaded. Unpaged on purpose: this answers
/// "what does this class study?" for the enrollment notification, and a class studies a
/// handful of courses, not a page of them.
/// </summary>
internal sealed class ClassCourseOfferingsForClassSpecification : Specification<ClassCourse>
{
    public ClassCourseOfferingsForClassSpecification(Guid classId)
    {
        ApplyNoTracking();
        AddInclude(cc => cc.Course);
        ApplyOrderBy(cc => cc.Course.Name);
        Criteria = cc => cc.ClassId == classId;
    }
}

internal sealed class ClassCourseDuplicateSpecification : Specification<ClassCourse>
{
    public ClassCourseDuplicateSpecification(Guid classId, Guid courseId)
    {
        Criteria = cc => cc.ClassId == classId && cc.CourseId == courseId;
    }
}

internal sealed class ClassCoursesPagedSpecification : Specification<ClassCourse>
{
    /// <summary>Columns this endpoint may be sorted by. See <see cref="SortMap{T}"/>.</summary>
    private static readonly SortMap<ClassCourse> Sortable = new(
        new Dictionary<string, System.Linq.Expressions.Expression<Func<ClassCourse, object>>>
        {
            ["class"] = cc => cc.Class.Level,
            ["course"] = cc => cc.Course.Name,
            ["courseCode"] = cc => cc.Course.Code,
            ["createdAt"] = cc => cc.CreatedAtUtc,
        },
        tieBreaker: cc => cc.Id);

    public ClassCoursesPagedSpecification(
        IEnumerable<Guid>? classIds, IEnumerable<Guid>? courseIds, IEnumerable<Guid>? teacherIds, string? search, string? sortBy, string? sortDir, int page, int pageSize)
    {
        ApplyNoTracking();
        AddInclude(cc => cc.Class);
        AddInclude(cc => cc.Course);
        // By grade then course name: an offering list is read class-by-class, and class
        // grade is a number, so it is ordered as one — otherwise 10 would sort before 9.
        if (!ApplySort(Sortable, sortBy, sortDir))
        {
            ApplyOrderBy(cc => cc.Class.Level);
            ApplyThenBy(cc => cc.Course.Name);
        }
        ApplyPaging(page, pageSize);

        var searchLower = search?.Trim().ToLowerInvariant();
        // A search term that is a whole number means a grade ("9"); anything else can only
        // be a section letter. Parsed once here so the grade arm switches itself off.
        var searchLevel = int.TryParse(searchLower, out var parsedLevel) ? parsedLevel : (int?)null;
        var classFilter = MultiValueFilter(classIds);
        var courseFilter = MultiValueFilter(courseIds);
        var teacherFilter = MultiValueFilter(teacherIds);

        // ToLower() (not ToLowerInvariant()) below: this Criteria is an expression tree that EF
        // Core translates to SQL LOWER(...), which ToLowerInvariant() cannot be translated to.
        // The column value never touches client culture, so the CA1304/CA1311 concern doesn't apply.
        //
        // The teacher filter is an EXISTS over teacher_assignments — an offering with a mapped
        // teacher is found under that teacher, and the unique index guarantees at most one.
#pragma warning disable CA1304, CA1311
        Criteria = cc =>
            (classFilter == null || classFilter.Contains(cc.ClassId)) &&
            (courseFilter == null || courseFilter.Contains(cc.CourseId)) &&
            (teacherFilter == null || cc.TeacherAssignments.Any(ta => teacherFilter.Contains(ta.TeacherId))) &&
            (string.IsNullOrWhiteSpace(searchLower) ||
             (searchLevel != null && cc.Class.Level == searchLevel) ||
             (cc.Class.Section != null && cc.Class.Section.ToLower().Contains(searchLower)) ||
             cc.Course.Name.ToLower().Contains(searchLower) ||
             cc.Course.Code.ToLower().Contains(searchLower));
#pragma warning restore CA1304, CA1311
    }
}
